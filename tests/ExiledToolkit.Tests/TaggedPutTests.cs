using ExiledToolkit;
using Xunit;
using static ExiledToolkit.TaggedPut;

namespace ExiledToolkit.Tests;

/// <summary>
/// A mod that puts things away for the player must only use storage the player tagged for them. A box with no tags, or
/// with tags for other things, is the player's own; filling it would mix up their home. When the tagged storage is full,
/// the player picks what happens, and what moved where has to be reported exactly, so nothing goes missing.
/// </summary>
public class TaggedPutTests
{
    private const long Source = 50;

    private static readonly Place Untagged = new(1, "Wooden Box");
    private static readonly Place Pantry = new(2, "Pantry", new Rank(Level: 3, Specificity: 1));
    private static readonly Place Fridge = new(3, "Fridge", new Rank(Level: 1, Specificity: 4));
    private static readonly Place Shelf = new(4, "Shelf", new Rank(Level: 0, Specificity: 1));

    private static readonly Place[] Bags = { new(90, "Shopping Bag"), new(91, "Shopping Bag 2") };

    /// <summary>A putIn that takes up to each place's room and records what it was asked.</summary>
    private sealed class Rooms
    {
        private readonly Dictionary<long, int> _room;

        public Rooms(params (long OwnerId, int Room)[] rooms) => _room = rooms.ToDictionary(room => room.OwnerId, room => room.Room);

        public List<(long OwnerId, int Asked)> Asked { get; } = new();

        public int PutIn(Place place, int count)
        {
            Asked.Add((place.OwnerId, count));

            return Math.Min(count, _room.GetValueOrDefault(place.OwnerId));
        }
    }

    [Fact]
    public void A_storage_with_no_tags_for_the_item_is_never_used()
    {
        var rooms = new Rooms((Untagged.OwnerId, 99), (Pantry.OwnerId, 99));

        var result = Put(new[] { Untagged, Pantry }, Bags, 5, NoRoom.ShoppingBag, Source, rooms.PutIn);

        Assert.Equal(new[] { new Part(Where.Stored, 5, Pantry.OwnerId, "Pantry") }, result.Parts);
        Assert.DoesNotContain(rooms.Asked, asked => asked.OwnerId == Untagged.OwnerId);
    }

    [Fact]
    public void Only_untagged_storage_means_nothing_is_stored_and_says_why()
    {
        var rooms = new Rooms((Untagged.OwnerId, 99), (90, 99));

        var result = Put(new[] { Untagged }, Bags, 5, NoRoom.ShoppingBag, Source, rooms.PutIn);

        Assert.Equal(Reason.NoTaggedStorage, result.Reason);
        Assert.Equal(new[] { new Part(Where.ShoppingBag, 5, 90, "Shopping Bag") }, result.Parts);
    }

    [Fact]
    public void The_most_specific_tag_wins_then_the_lower_level()
    {
        var targets = Targets(new[] { Pantry, Untagged, Shelf, Fridge });

        Assert.Equal(new[] { Fridge, Shelf, Pantry }, targets);
    }

    [Fact]
    public void Storages_that_rank_the_same_keep_the_order_they_came_in()
    {
        var second = Pantry with { OwnerId = 7, Name = "Pantry 2" };

        Assert.Equal(new[] { second, Pantry }, Targets(new[] { second, Pantry }));
    }

    [Fact]
    public void A_full_storage_spills_into_the_next_best_one()
    {
        var rooms = new Rooms((Fridge.OwnerId, 2), (Pantry.OwnerId, 99));

        var result = Put(new[] { Pantry, Fridge }, Bags, 5, NoRoom.ShoppingBag, Source, rooms.PutIn);

        Assert.Equal(new[] { new Part(Where.Stored, 2, Fridge.OwnerId, "Fridge"), new Part(Where.Stored, 3, Pantry.OwnerId, "Pantry") }, result.Parts);
        Assert.Equal(new[] { (Fridge.OwnerId, 5), (Pantry.OwnerId, 3) }, rooms.Asked);
        Assert.Equal(Reason.None, result.Reason);
        Assert.True(result.IsAllStored);
    }

    [Fact]
    public void With_the_shopping_bag_set_what_does_not_fit_goes_to_the_bags_in_order()
    {
        var rooms = new Rooms((Pantry.OwnerId, 1), (90, 2), (91, 99));

        var result = Put(new[] { Pantry }, Bags, 6, NoRoom.ShoppingBag, Source, rooms.PutIn);

        Assert.Equal(Reason.TaggedStorageFull, result.Reason);
        Assert.Equal(new[] { new Part(Where.Stored, 1, Pantry.OwnerId, "Pantry"), new Part(Where.ShoppingBag, 2, 90, "Shopping Bag"), new Part(Where.ShoppingBag, 3, 91, "Shopping Bag 2") }, result.Parts);
        Assert.False(result.IsAllStored);
    }

    [Fact]
    public void With_wait_for_room_set_what_does_not_fit_stays_where_it_is()
    {
        var rooms = new Rooms((Pantry.OwnerId, 1), (90, 99));

        var result = Put(new[] { Pantry }, Bags, 6, NoRoom.WaitForRoom, Source, rooms.PutIn);

        Assert.Equal(new[] { new Part(Where.Stored, 1, Pantry.OwnerId, "Pantry"), new Part(Where.Waiting, 5, Source, "") }, result.Parts);
        Assert.DoesNotContain(rooms.Asked, asked => asked.OwnerId == 90);
        Assert.Equal(Reason.TaggedStorageFull, result.Reason);
    }

    [Fact]
    public void Full_bags_leave_the_rest_waiting()
    {
        var rooms = new Rooms((Pantry.OwnerId, 1), (90, 1));

        var result = Put(new[] { Pantry }, Bags, 6, NoRoom.ShoppingBag, Source, rooms.PutIn);

        Assert.Equal(1, result.Count(Where.Stored));
        Assert.Equal(1, result.Count(Where.ShoppingBag));
        Assert.Equal(4, result.Count(Where.Waiting));
    }

    [Fact]
    public void A_place_that_says_it_took_more_than_asked_counts_as_taking_what_was_left()
    {
        var result = Put(new[] { Fridge, Pantry }, Bags, 4, NoRoom.WaitForRoom, Source, (_, _) => 10);

        Assert.Equal(new[] { new Part(Where.Stored, 4, Fridge.OwnerId, "Fridge") }, result.Parts);
    }

    [Fact]
    public void A_place_that_says_it_took_less_than_nothing_counts_as_nothing()
    {
        var result = Put(new[] { Pantry }, Bags, 4, NoRoom.WaitForRoom, Source, (_, _) => -3);

        Assert.Equal(new[] { new Part(Where.Waiting, 4, Source, "") }, result.Parts);
    }

    [Fact]
    public void Putting_nothing_asks_no_place_and_moves_nothing()
    {
        var rooms = new Rooms((Pantry.OwnerId, 99));

        var result = Put(new[] { Pantry }, Bags, 0, NoRoom.ShoppingBag, Source, rooms.PutIn);

        Assert.Empty(result.Parts);
        Assert.Empty(rooms.Asked);
        Assert.Equal(Reason.None, result.Reason);
    }

    [Fact]
    public void When_the_game_is_not_ready_all_of_it_waits_at_the_source()
    {
        var result = Result.NotReady(3, Source);

        Assert.Equal(new[] { new Part(Where.Waiting, 3, Source, "") }, result.Parts);
        Assert.Equal(Reason.NotReady, result.Reason);
    }

    [Fact]
    public void Repeated_storage_names_are_numbered_so_each_box_can_be_told_apart()
    {
        Assert.Equal(new[] { "Wooden Box", "Fridge", "Wooden Box 2", "Wooden Box 3" }, Numbered(new[] { "Wooden Box", "Fridge", "Wooden Box", "Wooden Box" }));
    }
}
