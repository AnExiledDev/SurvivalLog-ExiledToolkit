using ExiledToolkit.Game;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// Time Control reads this to decide whether to pause. A wrong answer either pauses a game someone else is playing in,
/// or stops pausing a game that one person plays alone, so a copy with no report must read as alone.
/// </summary>
[Collection(OtherPlayersCollection.Name)]
public class OtherPlayersTests
{
    [Fact]
    public void A_copy_nobody_reported_on_plays_alone()
    {
        OtherPlayers.Clear();

        Assert.Equal(0, OtherPlayers.Count);
        Assert.False(OtherPlayers.IsGuest);
        Assert.False(OtherPlayers.IsAnyBusy);
    }

    [Fact]
    public void A_host_with_a_busy_friend_reads_busy()
    {
        OtherPlayers.Report(count: 1, isGuest: false, isAnyBusy: true);

        Assert.Equal(1, OtherPlayers.Count);
        Assert.False(OtherPlayers.IsGuest);
        Assert.True(OtherPlayers.IsAnyBusy);

        OtherPlayers.Clear();
    }

    [Fact]
    public void Nobody_else_can_be_busy_when_nobody_else_plays()
    {
        OtherPlayers.Report(count: 0, isGuest: false, isAnyBusy: true);

        Assert.False(OtherPlayers.IsAnyBusy);
    }

    [Fact]
    public void A_guest_always_counts_the_host()
    {
        OtherPlayers.Report(count: 0, isGuest: true, isAnyBusy: false);

        Assert.Equal(1, OtherPlayers.Count);
        Assert.True(OtherPlayers.IsGuest);

        OtherPlayers.Clear();
    }

    [Fact]
    public void A_negative_count_reads_as_nobody()
    {
        OtherPlayers.Report(count: -3, isGuest: false, isAnyBusy: false);

        Assert.Equal(0, OtherPlayers.Count);
    }

    [Fact]
    public void Clearing_after_a_shared_game_plays_alone_again()
    {
        OtherPlayers.Report(count: 2, isGuest: true, isAnyBusy: true);
        OtherPlayers.Clear();

        Assert.Equal(0, OtherPlayers.Count);
        Assert.False(OtherPlayers.IsGuest);
        Assert.False(OtherPlayers.IsAnyBusy);
    }
}

/// <summary>The tests share one static, so they never run at the same time.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public class OtherPlayersCollection
{
    public const string Name = "OtherPlayers";
}
