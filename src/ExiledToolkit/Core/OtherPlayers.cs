using System;
using System.Collections.Generic;

namespace ExiledToolkit.Game;

/// <summary>
/// One message between copies of one mod. On the host <see cref="From"/> is the sender's character, by the host's id;
/// on a guest it is 0, the host. Toolkit 0.23.0 and newer.
/// </summary>
public sealed record PlayerMessage(string Channel, long From, byte[] Body);

/// <summary>
/// What a multiplayer mod gives the Toolkit so other mods can reach the other players. Only the multiplayer mod
/// implements this and connects it with <see cref="OtherPlayers.Connect"/>; other mods use <see cref="OtherPlayers"/>.
/// Every member is called on Unity's main thread unless it says otherwise. Toolkit 0.23.0 and newer.
/// </summary>
public interface IPlayerLink
{
    /// <summary>
    /// On the host, the character of the other player the game is acting for right now, by the host's id. Null for the
    /// host's own player and on a guest.
    /// </summary>
    long? ActingFor { get; }

    /// <summary>Sends to the host, from a guest. Messages on one channel arrive in order. False when it can't be sent.</summary>
    bool SendToHost(string channel, byte[] body);

    /// <summary>Sends to one other player, from the host. False when that player isn't connected.</summary>
    bool SendTo(long character, string channel, byte[] body);

    /// <summary>Sends to every other player, from the host. False when nobody got it.</summary>
    bool SendToEveryone(string channel, byte[] body);

    /// <summary>
    /// Runs <paramref name="run"/> as that player: their character in the player's seat and what the game shows going
    /// to their screen, then puts everything back. For the host's own character it just runs. False when the character
    /// isn't known and nothing ran.
    /// </summary>
    bool RunFor(long character, Action run);

    /// <summary>The host's id for an id in this copy (an agent or an item). Null when it isn't known.</summary>
    long? HostIdOf(long id);

    /// <summary>This copy's id for one of the host's ids. Null when it isn't known.</summary>
    long? LocalIdOf(long hostId);
}

/// <summary>
/// The other people playing in this game. A multiplayer mod reports here every frame while a game is shared and
/// connects an <see cref="IPlayerLink"/>. With neither, this copy plays alone and every value and call acts that way:
/// sends return false, <see cref="ActingFor"/> is null, <see cref="RunFor"/> just runs, and ids map to themselves.
/// Toolkit 0.22.0 and newer; the host, messages, ids and joins are 0.23.0 and newer.
/// </summary>
public static class OtherPlayers
{
    /// <summary>The biggest message body that may be sent, 64 KB. A bigger one is refused and the send returns false.</summary>
    public const int MaxBodyBytes = 64 * 1024;

    private static readonly object Gate = new();
    private static int _count;
    private static bool _isGuest;
    private static bool _isAnyBusy;
    private static IPlayerLink? _link;
    private static readonly Dictionary<string, List<Action<PlayerMessage>>> Listeners = new(StringComparer.Ordinal);
    private static readonly Queue<Arrival> Arrivals = new();

    /// <summary>Where a listener's failure is written. The Toolkit sets it to its log.</summary>
    internal static Action<string> Log = _ => { };

    /// <summary>How many other people play in this game. 0 when this copy plays alone.</summary>
    public static int Count
    {
        get { lock (Gate) return _count; }
    }

    /// <summary>
    /// True when this copy joined someone else's game. Its own world doesn't run: the host's game sets the time and the
    /// speed, so a mod here should leave both alone.
    /// </summary>
    public static bool IsGuest
    {
        get { lock (Gate) return _isGuest; }
    }

    /// <summary>
    /// True when other people play in this game and this copy is not a guest: the world runs here, so a mod that
    /// changes it does so here, for whoever asked. Toolkit 0.23.0 and newer.
    /// </summary>
    public static bool IsHost
    {
        get { lock (Gate) return _count > 0 && !_isGuest; }
    }

    /// <summary>True when another player has an order waiting or running, so the game is busy even if this player is not.</summary>
    public static bool IsAnyBusy
    {
        get { lock (Gate) return _isAnyBusy; }
    }

    /// <summary>
    /// On the host, the character of the other player the game is acting for right now: their action, their
    /// character's update, a call from their screen, or <see cref="RunFor"/>. Send what you would show to that player.
    /// Null for the host's own player, on a guest, and when alone. Read it on the main thread. Toolkit 0.23.0 and newer.
    /// </summary>
    public static long? ActingFor
    {
        get
        {
            IPlayerLink? link;

            lock (Gate)
            {
                if (_count == 0 || _isGuest) return null;

                link = _link;
            }

            return link?.ActingFor;
        }
    }

    /// <summary>
    /// Fires on the host, on the main thread, when another player joins, with their character by the host's id. Send
    /// them what your mod keeps that their copy needs. Toolkit 0.23.0 and newer.
    /// </summary>
    public static event Action<long>? Joined;

    /// <summary>Fires on the host, on the main thread, when another player leaves, with their character. Toolkit 0.23.0 and newer.</summary>
    public static event Action<long>? Left;

    /// <summary>
    /// What the multiplayer mod sees now. A guest always counts the host, so it has at least one other player. Nobody
    /// else can be busy when nobody else is playing.
    /// </summary>
    public static void Report(int count, bool isGuest, bool isAnyBusy)
    {
        var others = count < 0 ? 0 : count;
        if (isGuest && others == 0) others = 1;

        lock (Gate)
        {
            _count = others;
            _isGuest = isGuest;
            _isAnyBusy = others > 0 && isAnyBusy;
        }
    }

    /// <summary>The shared game ended: this copy plays alone again.</summary>
    public static void Clear() => Report(0, false, false);

    /// <summary>
    /// For the multiplayer mod: the link every call here goes through. A second link replaces the first. Toolkit 0.23.0
    /// and newer.
    /// </summary>
    public static void Connect(IPlayerLink link)
    {
        if (link == null) throw new ArgumentNullException(nameof(link));

        lock (Gate)
        {
            _link = link;
            Arrivals.Clear();
        }
    }

    /// <summary>For the multiplayer mod: takes its link off. Does nothing when another link is connected. Toolkit 0.23.0 and newer.</summary>
    public static void Disconnect(IPlayerLink link)
    {
        lock (Gate)
        {
            if (!ReferenceEquals(_link, link)) return;

            _link = null;
            Arrivals.Clear();
        }
    }

    /// <summary>
    /// Calls <paramref name="heard"/> on the main thread for every message on <paramref name="channel"/>. Pick a channel
    /// no other mod uses: your plugin GUID, a dot and a word. Dispose what it returns to stop listening. Safe from any
    /// thread. Toolkit 0.23.0 and newer.
    /// </summary>
    public static IDisposable Listen(string channel, Action<PlayerMessage> heard)
    {
        if (channel == null) throw new ArgumentNullException(nameof(channel));
        if (heard == null) throw new ArgumentNullException(nameof(heard));

        lock (Gate)
        {
            if (!Listeners.TryGetValue(channel, out var list)) Listeners[channel] = list = new List<Action<PlayerMessage>>();

            list.Add(heard);
        }

        return new Listening(channel, heard);
    }

    /// <summary>
    /// From a guest, sends <paramref name="body"/> to this mod's copy on the host. Messages on one channel arrive in
    /// order and none is dropped while the players stay connected. False on the host, when alone, and for a body over
    /// <see cref="MaxBodyBytes"/>. Toolkit 0.23.0 and newer.
    /// </summary>
    public static bool SendToHost(string channel, byte[] body)
    {
        var link = LinkFor(isGuest: true, channel, body);

        return link != null && link.SendToHost(channel, body);
    }

    /// <summary>
    /// From the host, sends <paramref name="body"/> to one other player's copy of this mod. <paramref name="character"/>
    /// is the host's id for their character (<see cref="ActingFor"/>, <see cref="Joined"/>). Same order and size rules as
    /// <see cref="SendToHost"/>. False on a guest, when alone, and when that player isn't connected. Toolkit 0.23.0 and newer.
    /// </summary>
    public static bool SendTo(long character, string channel, byte[] body)
    {
        var link = LinkFor(isGuest: false, channel, body);

        return link != null && link.SendTo(character, channel, body);
    }

    /// <summary>From the host, sends <paramref name="body"/> to every other player. False on a guest and when alone. Toolkit 0.23.0 and newer.</summary>
    public static bool SendToEveryone(string channel, byte[] body)
    {
        var link = LinkFor(isGuest: false, channel, body);

        return link != null && link.SendToEveryone(channel, body);
    }

    /// <summary>
    /// On the host, runs <paramref name="run"/> as another player: their character in the player's seat, and what the
    /// game shows going to their screen. For the host's own character, on a guest and when alone it just runs. False
    /// when the character isn't known and nothing ran. Main thread only. Toolkit 0.23.0 and newer.
    /// </summary>
    public static bool RunFor(long character, Action run)
    {
        if (run == null) throw new ArgumentNullException(nameof(run));

        var link = HostLink();

        if (link == null)
        {
            run();

            return true;
        }

        return link.RunFor(character, run);
    }

    /// <summary>
    /// The host's id for an agent (a character, furniture, a robot) or an item in this copy. On the host and when alone
    /// it is the same id. Null when the id isn't known. Toolkit 0.23.0 and newer.
    /// </summary>
    public static long? HostIdOf(long id) => GuestLink() is { } link ? link.HostIdOf(id) : id;

    /// <summary>This copy's id for one of the host's ids. On the host and when alone it is the same id. Null when it isn't known. Toolkit 0.23.0 and newer.</summary>
    public static long? LocalIdOf(long hostId) => GuestLink() is { } link ? link.LocalIdOf(hostId) : hostId;

    /// <summary>For the multiplayer mod: a message came in. Safe from any thread; listeners hear it at the next frame, in order.</summary>
    public static void Deliver(IPlayerLink link, PlayerMessage message)
    {
        if (message?.Channel == null || message.Body == null) return;

        Enqueue(link, new Arrival(message, 0, false));
    }

    /// <summary>For the multiplayer mod, on the host: a player joined. Safe from any thread; <see cref="Joined"/> fires at the next frame.</summary>
    public static void ReportJoined(IPlayerLink link, long character) => Enqueue(link, new Arrival(null, character, true));

    /// <summary>For the multiplayer mod, on the host: a player left. Safe from any thread; <see cref="Left"/> fires at the next frame.</summary>
    public static void ReportLeft(IPlayerLink link, long character) => Enqueue(link, new Arrival(null, character, false));

    /// <summary>Main thread, once a frame: hands what came in to the listeners and the join and leave events, oldest first.</summary>
    internal static void Pump()
    {
        while (true)
        {
            Arrival arrival;
            Action<PlayerMessage>[] heard = Array.Empty<Action<PlayerMessage>>();

            lock (Gate)
            {
                if (Arrivals.Count == 0) return;

                arrival = Arrivals.Dequeue();

                if (arrival.Message != null && Listeners.TryGetValue(arrival.Message.Channel, out var list)) heard = list.ToArray();
            }

            if (arrival.Message is { } message)
            {
                foreach (var listener in heard) Call(() => listener(message), "a listener on " + message.Channel);
            }
            else
            {
                var handler = arrival.IsJoin ? Joined : Left;

                if (handler != null) Call(() => handler(arrival.Character), arrival.IsJoin ? "a Joined handler" : "a Left handler");
            }
        }
    }

    private static void Enqueue(IPlayerLink link, Arrival arrival)
    {
        lock (Gate)
        {
            if (link == null || !ReferenceEquals(_link, link)) return;

            Arrivals.Enqueue(arrival);
        }
    }

    private static void Call(Action call, string what)
    {
        try
        {
            call();
        }
        catch (Exception ex)
        {
            Log($"{what} failed: {ex}");
        }
    }

    private static IPlayerLink? LinkFor(bool isGuest, string channel, byte[] body)
    {
        if (channel == null || body == null || body.Length > MaxBodyBytes) return null;

        return isGuest ? GuestLink() : HostLink();
    }

    private static IPlayerLink? HostLink()
    {
        lock (Gate) return _count > 0 && !_isGuest ? _link : null;
    }

    private static IPlayerLink? GuestLink()
    {
        lock (Gate) return _isGuest ? _link : null;
    }

    private static void StopListening(string channel, Action<PlayerMessage> heard)
    {
        lock (Gate)
        {
            if (!Listeners.TryGetValue(channel, out var list)) return;

            list.Remove(heard);

            if (list.Count == 0) Listeners.Remove(channel);
        }
    }

    private sealed record Arrival(PlayerMessage? Message, long Character, bool IsJoin);

    private sealed class Listening : IDisposable
    {
        private string? _channel;
        private readonly Action<PlayerMessage> _heard;

        public Listening(string channel, Action<PlayerMessage> heard)
        {
            _channel = channel;
            _heard = heard;
        }

        public void Dispose()
        {
            var channel = _channel;
            _channel = null;

            if (channel != null) StopListening(channel, _heard);
        }
    }
}
