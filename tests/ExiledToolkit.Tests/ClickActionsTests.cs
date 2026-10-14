using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>The kept click actions: bounded, newest kept, and a toast that keeps coming back stays openable.</summary>
public class ClickActionsTests
{
    [Fact]
    public void Past_capacity_the_oldest_action_is_forgotten_first()
    {
        var actions = new ClickActions(capacity: 2);

        actions.Set(new ToastId(1), () => { });
        actions.Set(new ToastId(2), () => { });
        actions.Set(new ToastId(3), () => { });

        Assert.False(actions.TryGet(new ToastId(1), out _));
        Assert.True(actions.TryGet(new ToastId(2), out _));
        Assert.True(actions.TryGet(new ToastId(3), out _));
    }

    [Fact]
    public void A_toast_given_a_new_action_counts_as_newest_so_a_repeating_toast_is_not_forgotten()
    {
        var actions = new ClickActions(capacity: 2);

        actions.Set(new ToastId(1), () => { });
        actions.Set(new ToastId(2), () => { });
        actions.Set(new ToastId(1), () => { });
        actions.Set(new ToastId(3), () => { });

        Assert.True(actions.TryGet(new ToastId(1), out _));
        Assert.False(actions.TryGet(new ToastId(2), out _));
    }

    [Fact]
    public void Toasts_without_an_action_take_no_room()
    {
        var actions = new ClickActions(capacity: 1);

        actions.Set(new ToastId(1), () => { });
        actions.Set(new ToastId(2), null);

        Assert.True(actions.TryGet(new ToastId(1), out _));
        Assert.Equal(1, actions.Count);
    }
}
