using System.Diagnostics;
using ExiledToolkit.Web;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// The one hook on the game's web messages, shared by every mod: each message goes to the mod that listens for it and
/// to no one else, and anything no mod listens for goes on to the game. The inbox is shared, so each test uses
/// prefixes of its own.
/// </summary>
public class PageInboxTests
{
    [Fact]
    public void A_message_goes_to_the_mailbox_of_its_prefix_and_is_kept_from_the_game()
    {
        var farm = PageInbox.Listen("slmod|t-farm|");
        var trap = PageInbox.Listen("slmod|t-trap|");

        Assert.True(PageInbox.Route("slmod|t-farm|open"));
        Assert.True(farm.TryTake(out var message));
        Assert.Equal("slmod|t-farm|open", message);
        Assert.False(trap.HasWaiting);
    }

    [Fact]
    public void A_message_no_mod_listens_for_goes_on_to_the_game()
    {
        PageInbox.Listen("slmod|t-only|");

        Assert.False(PageInbox.Route("slmod|t-other|open"));
        Assert.False(PageInbox.Route("{\"type\":\"game\"}"));
        Assert.False(PageInbox.Route(null));
    }

    [Fact]
    public void Asking_twice_for_one_prefix_gives_the_same_mailbox()
    {
        Assert.Same(PageInbox.Listen("slmod|t-same|"), PageInbox.Listen("slmod|t-same|"));
    }

    [Fact]
    public void The_longest_matching_prefix_wins_whichever_listened_first()
    {
        var window = PageInbox.Listen("slmod|t-long|");
        var timer = PageInbox.Listen("slmod|t-long|timer|");

        PageInbox.Route("slmod|t-long|timer|noscript");
        PageInbox.Route("slmod|t-long|open");

        Assert.True(timer.TryTake(out var toTimer));
        Assert.Equal("slmod|t-long|timer|noscript", toTimer);
        Assert.True(window.TryTake(out var toWindow));
        Assert.Equal("slmod|t-long|open", toWindow);
        Assert.False(window.HasWaiting);
    }

    [Fact]
    public void One_mailbox_can_take_several_prefixes_in_the_order_they_came()
    {
        var mailbox = PageInbox.Listen("slmod|t-tab|", "slmod|t-hud|");

        PageInbox.Route("slmod|t-hud|click");
        PageInbox.Route("slmod|t-tab|ready");

        Assert.Equal(new[] { "slmod|t-tab|", "slmod|t-hud|" }, mailbox.Prefixes);
        Assert.Same(mailbox, PageInbox.Listen("slmod|t-hud|"));
        Assert.True(mailbox.TryTake(out var first));
        Assert.True(mailbox.TryTake(out var second));
        Assert.Equal(("slmod|t-hud|click", "slmod|t-tab|ready"), (first, second));
    }

    [Fact]
    public void A_prefix_added_to_a_mailbox_joins_the_one_that_already_has_the_other()
    {
        var first = PageInbox.Listen("slmod|t-join-a|");

        Assert.Same(first, PageInbox.Listen("slmod|t-join-a|", "slmod|t-join-b|"));
        Assert.True(PageInbox.Route("slmod|t-join-b|x"));
        Assert.True(first.HasWaiting);
    }

    [Fact]
    public void Prefixes_of_two_different_mailboxes_cannot_be_joined()
    {
        PageInbox.Listen("slmod|t-two-a|");
        PageInbox.Listen("slmod|t-two-b|");

        Assert.Throws<ArgumentException>(() => PageInbox.Listen("slmod|t-two-a|", "slmod|t-two-b|"));
    }

    [Fact]
    public void An_empty_prefix_would_take_every_message_so_it_is_refused()
    {
        Assert.Throws<ArgumentException>(() => PageInbox.Listen(""));
        Assert.Throws<ArgumentException>(() => PageInbox.Listen(Array.Empty<string>()));
    }

    [Fact]
    public void A_message_says_when_it_arrived()
    {
        var mailbox = PageInbox.Listen("slmod|t-stamp|");
        var before = Stopwatch.GetTimestamp();

        PageInbox.Route("slmod|t-stamp|pong");

        Assert.True(mailbox.TryTake(out _, out var arrivedAt));
        Assert.InRange(arrivedAt, before, Stopwatch.GetTimestamp());
    }

    [Fact]
    public void A_handler_gets_each_message_when_the_frame_delivers_them()
    {
        var heard = new List<string>();

        PageInbox.Listen("slmod|t-handler|", heard.Add);
        PageInbox.Route("slmod|t-handler|a");
        PageInbox.Route("slmod|t-handler|b");

        Assert.Empty(heard);

        PageInbox.Deliver();

        Assert.Equal(new[] { "slmod|t-handler|a", "slmod|t-handler|b" }, heard);
    }

    [Fact]
    public void A_handler_that_throws_does_not_stop_its_next_message_or_another_mod()
    {
        var heard = new List<string>();

        PageInbox.Listen("slmod|t-throws|", message => throw new InvalidOperationException(message));
        PageInbox.Listen("slmod|t-fine|", heard.Add);
        PageInbox.Route("slmod|t-throws|1");
        PageInbox.Route("slmod|t-throws|2");
        PageInbox.Route("slmod|t-fine|ok");

        PageInbox.Deliver();

        Assert.Equal(new[] { "slmod|t-fine|ok" }, heard);
        Assert.False(PageInbox.Listen("slmod|t-throws|").HasWaiting);
    }

    [Fact]
    public void An_empty_mailbox_hands_out_nothing()
    {
        var mailbox = PageInbox.Listen("slmod|t-empty|");

        Assert.False(mailbox.TryTake(out var message));
        Assert.Equal("", message);
    }
}
