using ExiledToolkit.Core;
using ExiledToolkit.Game;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// A link the tests drive by hand. It records what was sent and run, and plays the multiplayer mod's part of
/// <see cref="OtherPlayers.RunFor"/>: <see cref="ActingFor"/> is the friend while their action runs.
/// </summary>
internal sealed class FakeLink : IPlayerLink
{
    public const long Friend = 42;

    public long? ActingFor { get; set; }

    public bool IsConnected { get; set; } = true;

    public List<(long? To, string Channel, byte[] Body)> Sent { get; } = new();

    public List<long> RanFor { get; } = new();

    public Dictionary<long, long> HostIds { get; } = new();

    public bool SendToHost(string channel, byte[] body) => Record(null, channel, body);

    public bool SendTo(long character, string channel, byte[] body) => Record(character, channel, body);

    public bool SendToEveryone(string channel, byte[] body) => Record(-1, channel, body);

    public bool RunFor(long character, Action run)
    {
        var before = ActingFor;

        RanFor.Add(character);
        ActingFor = character;

        try
        {
            run();
        }
        finally
        {
            ActingFor = before;
        }

        return true;
    }

    public long? HostIdOf(long id) => HostIds.TryGetValue(id, out var hostId) ? hostId : null;

    public long? LocalIdOf(long hostId)
    {
        foreach (var (local, host) in HostIds)
        {
            if (host == hostId) return local;
        }

        return null;
    }

    public Exception? SendFails { get; set; }

    private bool Record(long? to, string channel, byte[] body)
    {
        if (SendFails != null) throw SendFails;
        if (!IsConnected) return false;

        Sent.Add((to, channel, body));

        return true;
    }
}

/// <summary>
/// The players API other mods build on. A copy with no multiplayer mod must act exactly as a game played alone, and in a
/// shared game a message must reach only the copy it was sent to, and only from the right side.
/// </summary>
[Collection(OtherPlayersCollection.Name)]
public sealed class PlayerLinkTests : IDisposable
{
    private readonly FakeLink _link = new();

    public PlayerLinkTests()
    {
        OtherPlayers.Clear();
    }

    public void Dispose()
    {
        OtherPlayers.Disconnect(_link);
        OtherPlayers.Clear();
    }

    [Fact]
    public void Alone_every_call_acts_as_a_game_played_alone()
    {
        var ran = false;

        Assert.False(OtherPlayers.IsHost);
        Assert.Null(OtherPlayers.ActingFor);
        Assert.False(OtherPlayers.SendToHost("mod.word", new byte[] { 1 }));
        Assert.False(OtherPlayers.SendTo(FakeLink.Friend, "mod.word", new byte[] { 1 }));
        Assert.False(OtherPlayers.SendToEveryone("mod.word", new byte[] { 1 }));
        Assert.True(OtherPlayers.RunFor(FakeLink.Friend, () => ran = true));
        Assert.True(ran);
        Assert.Equal(7, OtherPlayers.HostIdOf(7));
        Assert.Equal(7, OtherPlayers.LocalIdOf(7));
    }

    [Fact]
    public void A_connected_link_does_nothing_until_someone_else_plays()
    {
        OtherPlayers.Connect(_link);
        _link.ActingFor = FakeLink.Friend;

        Assert.Null(OtherPlayers.ActingFor);
        Assert.False(OtherPlayers.SendTo(FakeLink.Friend, "mod.word", new byte[] { 1 }));
        Assert.Empty(_link.Sent);
    }

    [Fact]
    public void A_host_sends_to_a_friend_and_everyone_but_never_to_the_host()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: false, isAnyBusy: false);

        Assert.True(OtherPlayers.IsHost);
        Assert.True(OtherPlayers.SendTo(FakeLink.Friend, "mod.word", new byte[] { 1 }));
        Assert.True(OtherPlayers.SendToEveryone("mod.word", new byte[] { 2 }));
        Assert.False(OtherPlayers.SendToHost("mod.word", new byte[] { 3 }));
        Assert.Equal(new long?[] { FakeLink.Friend, -1 }, _link.Sent.Select(sent => sent.To));
    }

    [Fact]
    public void A_guest_sends_only_to_the_host()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: true, isAnyBusy: false);

        Assert.False(OtherPlayers.IsHost);
        Assert.True(OtherPlayers.SendToHost("mod.word", new byte[] { 1 }));
        Assert.False(OtherPlayers.SendTo(FakeLink.Friend, "mod.word", new byte[] { 2 }));
        Assert.False(OtherPlayers.SendToEveryone("mod.word", new byte[] { 3 }));
        Assert.Equal(new long?[] { null }, _link.Sent.Select(sent => sent.To));
    }

    [Fact]
    public void A_body_over_64_KB_is_refused()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: false, isAnyBusy: false);

        Assert.True(OtherPlayers.SendTo(FakeLink.Friend, "mod.word", new byte[OtherPlayers.MaxBodyBytes]));
        Assert.False(OtherPlayers.SendTo(FakeLink.Friend, "mod.word", new byte[OtherPlayers.MaxBodyBytes + 1]));
        Assert.Single(_link.Sent);
    }

    [Fact]
    public void A_guest_reads_no_one_it_acts_for_even_if_the_link_says_so()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: true, isAnyBusy: false);
        _link.ActingFor = FakeLink.Friend;

        Assert.Null(OtherPlayers.ActingFor);
    }

    [Fact]
    public void On_a_guest_ids_go_through_the_link_and_an_unknown_one_is_null()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: true, isAnyBusy: false);
        _link.HostIds[5] = 500;

        Assert.Equal(500, OtherPlayers.HostIdOf(5));
        Assert.Equal(5, OtherPlayers.LocalIdOf(500));
        Assert.Null(OtherPlayers.HostIdOf(6));
    }

    [Fact]
    public void On_the_host_ids_are_the_same_ids()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: false, isAnyBusy: false);

        Assert.Equal(9, OtherPlayers.HostIdOf(9));
        Assert.Equal(9, OtherPlayers.LocalIdOf(9));
    }

    [Fact]
    public void Run_for_on_the_host_goes_through_the_link_as_that_friend()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: false, isAnyBusy: false);
        long? actingInside = null;

        OtherPlayers.RunFor(FakeLink.Friend, () => actingInside = OtherPlayers.ActingFor);

        Assert.Equal(FakeLink.Friend, actingInside);
        Assert.Equal(new[] { FakeLink.Friend }, _link.RanFor);
        Assert.Null(OtherPlayers.ActingFor);
    }

    [Fact]
    public void Listeners_hear_their_channel_in_order_at_the_next_pump_and_stop_when_disposed()
    {
        var heard = new List<byte>();
        var other = new List<byte>();
        OtherPlayers.Connect(_link);
        var listening = OtherPlayers.Listen("mod.word", message => heard.Add(message.Body[0]));
        using var otherListening = OtherPlayers.Listen("other.word", message => other.Add(message.Body[0]));

        OtherPlayers.Deliver(_link, new PlayerMessage("mod.word", 0, new byte[] { 1 }));
        OtherPlayers.Deliver(_link, new PlayerMessage("mod.word", 0, new byte[] { 2 }));
        var beforePump = heard.Count;
        OtherPlayers.Pump();
        listening.Dispose();
        OtherPlayers.Deliver(_link, new PlayerMessage("mod.word", 0, new byte[] { 3 }));
        OtherPlayers.Pump();

        Assert.Equal(0, beforePump);
        Assert.Equal(new byte[] { 1, 2 }, heard);
        Assert.Empty(other);
    }

    [Fact]
    public void A_link_that_is_not_connected_cannot_deliver()
    {
        var heard = 0;
        var stranger = new FakeLink();
        OtherPlayers.Connect(_link);
        using var listening = OtherPlayers.Listen("mod.word", _ => heard++);

        OtherPlayers.Deliver(stranger, new PlayerMessage("mod.word", 0, new byte[] { 1 }));
        OtherPlayers.Disconnect(stranger);
        OtherPlayers.Pump();

        Assert.Equal(0, heard);
    }

    [Fact]
    public void A_listener_that_throws_does_not_stop_the_next_one()
    {
        var heard = 0;
        OtherPlayers.Connect(_link);
        using var failing = OtherPlayers.Listen("mod.word", _ => throw new InvalidOperationException("boom"));
        using var listening = OtherPlayers.Listen("mod.word", _ => heard++);

        OtherPlayers.Deliver(_link, new PlayerMessage("mod.word", 0, new byte[] { 1 }));
        OtherPlayers.Pump();

        Assert.Equal(1, heard);
    }

    [Fact]
    public void Joins_and_leaves_fire_at_the_next_pump_with_the_friend()
    {
        var events = new List<string>();
        Action<long> joined = character => events.Add("joined " + character);
        Action<long> left = character => events.Add("left " + character);
        OtherPlayers.Connect(_link);
        OtherPlayers.Joined += joined;
        OtherPlayers.Left += left;

        try
        {
            OtherPlayers.ReportJoined(_link, FakeLink.Friend);
            OtherPlayers.ReportLeft(_link, FakeLink.Friend);
            OtherPlayers.Pump();
        }
        finally
        {
            OtherPlayers.Joined -= joined;
            OtherPlayers.Left -= left;
        }

        Assert.Equal(new[] { "joined 42", "left 42" }, events);
    }
}

/// <summary>
/// A toast the host shows while acting for a friend must show on that friend's screen and nobody else's, and their click
/// must run the action on the host, as them. One test process plays both copies by switching what it reports.
/// </summary>
[Collection(OtherPlayersCollection.Name)]
public sealed class ToastRelayTests : IDisposable
{
    private readonly FakeLink _link = new();
    private readonly List<ToastRequest> _shownHere = new();
    private bool _onMainThread = true;
    private readonly ToastRelay _relay;
    private readonly IDisposable _listening;

    public ToastRelayTests()
    {
        OtherPlayers.Clear();
        _relay = new ToastRelay(request =>
        {
            _shownHere.Add(request!);

            return new ToastId(_shownHere.Count);
        }, () => _onMainThread, _ => { });
        _listening = _relay.Listen();
    }

    public void Dispose()
    {
        _listening.Dispose();
        OtherPlayers.Disconnect(_link);
        OtherPlayers.Clear();
    }

    [Fact]
    public void Alone_a_toast_shows_here()
    {
        var id = _relay.Show(Harness.Stamina());

        Assert.Equal(new ToastId(1), id);
        Assert.Single(_shownHere);
    }

    [Fact]
    public void A_toast_from_another_thread_shows_here_even_while_the_host_acts_for_a_friend()
    {
        BeHost();
        _link.ActingFor = FakeLink.Friend;
        _onMainThread = false;

        var id = _relay.Show(Harness.Stamina());

        Assert.Equal(new ToastId(1), id);
        Assert.Single(_shownHere);
        Assert.Empty(_link.Sent);
    }

    [Fact]
    public void A_toast_the_link_fails_to_send_shows_here_instead_of_throwing_into_the_mod()
    {
        BeHost();
        _link.ActingFor = FakeLink.Friend;
        _link.SendFails = new InvalidOperationException("collection was modified");

        var id = _relay.Show(Harness.Stamina());

        Assert.Equal(new ToastId(1), id);
        Assert.Single(_shownHere);
    }

    [Fact]
    public void A_toast_with_a_broken_string_length_is_not_read()
    {
        var body = new byte[] { 1, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF };

        Assert.False(ToastRelay.TryRead(body, out _, out _));
    }

    [Fact]
    public void On_the_host_acting_for_nobody_a_toast_shows_here()
    {
        BeHost();

        _relay.Show(Harness.Stamina());

        Assert.Single(_shownHere);
        Assert.Empty(_link.Sent);
    }

    [Fact]
    public void On_the_host_acting_for_a_friend_the_toast_goes_to_that_friend_only()
    {
        BeHost();
        _link.ActingFor = FakeLink.Friend;

        var id = _relay.Show(Harness.Stamina() with { Seconds = 12, Key = "rest" });

        Assert.Equal(ToastId.None, id);
        Assert.Empty(_shownHere);
        var sent = Assert.Single(_link.Sent);
        Assert.Equal(FakeLink.Friend, sent.To);
        Assert.Equal(ToastRelay.ToastChannel, sent.Channel);
    }

    [Fact]
    public void The_friend_sees_the_toast_as_the_host_made_it()
    {
        var made = Harness.Stamina() with { Seconds = 12, Key = "rest", Items = new[] { new ToastItem("Wood") { Icon = "icons/wood.png", Count = 3 } } };
        BeHost();
        _link.ActingFor = FakeLink.Friend;
        _relay.Show(made);
        var body = _link.Sent[0].Body;

        BeGuest();
        OtherPlayers.Deliver(_link, new PlayerMessage(ToastRelay.ToastChannel, 0, body));
        OtherPlayers.Pump();

        var shown = Assert.Single(_shownHere);
        Assert.Equal(made.Source, shown.Source);
        Assert.Equal(made.Title, shown.Title);
        Assert.Equal(made.Body, shown.Body);
        Assert.Equal(made.Kind, shown.Kind);
        Assert.Equal(made.Icon, shown.Icon);
        Assert.Equal(12, shown.Seconds);
        Assert.Equal("rest", shown.Key);
        Assert.Equal(made.Items, shown.Items);
        Assert.Null(shown.OnClick);
    }

    [Fact]
    public void The_friends_click_runs_the_action_on_the_host_as_that_friend()
    {
        long? clickedAs = null;
        BeHost();
        _link.ActingFor = FakeLink.Friend;
        _relay.Show(Harness.Stamina() with { OnClick = () => clickedAs = OtherPlayers.ActingFor });
        _link.ActingFor = null;
        var toast = _link.Sent[0].Body;

        BeGuest();
        OtherPlayers.Deliver(_link, new PlayerMessage(ToastRelay.ToastChannel, 0, toast));
        OtherPlayers.Pump();
        _shownHere[0].OnClick!();
        var click = _link.Sent[1];

        BeHost();
        OtherPlayers.Deliver(_link, new PlayerMessage(click.Channel, FakeLink.Friend, click.Body));
        OtherPlayers.Pump();

        Assert.Null(click.To);
        Assert.Equal(ToastRelay.ClickChannel, click.Channel);
        Assert.Equal(FakeLink.Friend, clickedAs);
        Assert.Equal(new[] { FakeLink.Friend }, _link.RanFor);
    }

    [Fact]
    public void Another_player_cannot_click_a_toast_sent_to_someone_else()
    {
        var clicks = 0;
        BeHost();
        _link.ActingFor = FakeLink.Friend;
        _relay.Show(Harness.Stamina() with { OnClick = () => clicks++ });
        _link.ActingFor = null;
        BeGuest();
        OtherPlayers.Deliver(_link, new PlayerMessage(ToastRelay.ToastChannel, 0, _link.Sent[0].Body));
        OtherPlayers.Pump();
        _shownHere[0].OnClick!();
        var click = _link.Sent[1];

        BeHost();
        OtherPlayers.Deliver(_link, new PlayerMessage(click.Channel, FakeLink.Friend + 1, click.Body));
        OtherPlayers.Pump();

        Assert.Equal(0, clicks);
    }

    [Fact]
    public void The_host_ignores_a_toast_sent_to_it()
    {
        BeHost();
        OtherPlayers.Deliver(_link, new PlayerMessage(ToastRelay.ToastChannel, FakeLink.Friend, ToastRelay.Write(Harness.Stamina(), 0)));
        OtherPlayers.Pump();

        Assert.Empty(_shownHere);
    }

    [Fact]
    public void A_toast_in_another_format_is_not_shown()
    {
        var body = ToastRelay.Write(Harness.Stamina(), 0);
        body[0] = 99;
        BeGuest();

        OtherPlayers.Deliver(_link, new PlayerMessage(ToastRelay.ToastChannel, 0, body));
        OtherPlayers.Deliver(_link, new PlayerMessage(ToastRelay.ToastChannel, 0, new byte[] { 1, 2 }));
        OtherPlayers.Pump();

        Assert.Empty(_shownHere);
    }

    private void BeHost()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: false, isAnyBusy: false);
    }

    private void BeGuest()
    {
        OtherPlayers.Connect(_link);
        OtherPlayers.Report(count: 1, isGuest: true, isAnyBusy: false);
    }
}
