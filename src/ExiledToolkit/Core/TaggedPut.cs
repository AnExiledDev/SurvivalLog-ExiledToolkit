using System;
using System.Collections.Generic;
using System.Linq;

namespace ExiledToolkit;

/// <summary>
/// Where an item goes when a mod puts it away the way the player would want: only into home storage whose tags ask for
/// it. A storage with no tags, or with tags for something else, is never picked, so a mod never fills a box the player
/// keeps for other things. The game side is <c>ExiledToolkit.Game.TaggedStorage</c>.
/// </summary>
public static class TaggedPut
{
    /// <summary>What to do with what no tagged storage has room for.</summary>
    public enum NoRoom
    {
        /// <summary>Put it in the shopping bags by the front door, where the player picks it up.</summary>
        ShoppingBag,

        /// <summary>Leave it where it is until a tagged storage has room.</summary>
        WaitForRoom,
    }

    public enum Where
    {
        Stored,
        ShoppingBag,
        Waiting,
    }

    /// <summary>Why some of it didn't go into tagged storage.</summary>
    public enum Reason
    {
        None,

        /// <summary>No home storage is tagged for the item.</summary>
        NoTaggedStorage,

        /// <summary>Every storage tagged for the item is full.</summary>
        TaggedStorageFull,

        /// <summary>The game wasn't ready (no save loaded, a read failed), so nothing moved.</summary>
        NotReady,
    }

    /// <summary>How much a storage's tags want an item, as the game's TryGetPutRank reports it.</summary>
    public readonly record struct Rank(int Level, int Specificity);

    /// <summary>A storage or shopping bag an item can go into. Rank is null when the storage's tags don't want the item.</summary>
    public sealed record Place(long OwnerId, string Name, Rank? Rank = null);

    /// <summary>
    /// A home storage that Move and Add can pick, named the way they report it. Tags is the storage's tag list, empty
    /// when it has none and for a material rack, which ranks by its slots. UsedCells of TotalCells is how full the game
    /// shows it over the storage (rows for a rack).
    /// </summary>
    public sealed record Storage(long OwnerId, int FurnitureConfigId, string Name, IReadOnlyList<int> Tags, int UsedCells, int TotalCells);

    /// <summary>
    /// A stack in one of the shopping bags by the front door. Bag is 1 or 2. Tags are the storage tags that would take
    /// it, in the order the game's tag picker lists them. HasStorage is whether some home storage is tagged for it now.
    /// </summary>
    public sealed record BagStack(int Bag, long BagOwnerId, long ItemInstanceId, int ItemConfigId, int Count, IReadOnlyList<int> Tags, bool HasStorage);

    /// <summary>Some of the item and where it ended up. A Waiting part's owner is where it still is, 0 when it was never made.</summary>
    public sealed record Part(Where Where, int Count, long OwnerId, string Name);

    public sealed record Result(IReadOnlyList<Part> Parts, Reason Reason)
    {
        public int Count(Where where) => Parts.Where(part => part.Where == where).Sum(part => part.Count);

        public bool IsAllStored => Parts.All(part => part.Where == Where.Stored);

        /// <summary>Nothing moved because the game wasn't ready: all of it waits where it is.</summary>
        public static Result NotReady(int count, long sourceOwnerId) =>
            new(count > 0 ? new[] { new Part(Where.Waiting, count, sourceOwnerId, "") } : Array.Empty<Part>(), Reason.NotReady);
    }

    /// <summary>
    /// The storages whose tags want the item, best first: the most specific tag rule wins, then the lower level, the
    /// order the game's robots use. Storages that rank the same keep the order they came in.
    /// </summary>
    public static IReadOnlyList<Place> Targets(IEnumerable<Place> candidates) =>
        candidates
            .Where(candidate => candidate.Rank.HasValue)
            .OrderByDescending(candidate => candidate.Rank.GetValueOrDefault().Specificity)
            .ThenBy(candidate => candidate.Rank.GetValueOrDefault().Level)
            .ToList();

    /// <summary>
    /// Puts count of an item away. It fills the tagged storages best first, then with NoRoom.ShoppingBag the shopping
    /// bags, and whatever is left waits at the source. putIn moves up to the asked count into one place and says how many
    /// really went in; it is never asked for more than is left, and an answer above that counts as all of it.
    /// </summary>
    public static Result Put(IEnumerable<Place> candidates, IReadOnlyList<Place> shoppingBags, int count, NoRoom noRoom, long sourceOwnerId, Func<Place, int, int> putIn)
    {
        var targets = Targets(candidates);
        var parts = new List<Part>();

        var left = Fill(targets, Where.Stored, count, putIn, parts);

        if (left == 0) return new Result(parts, Reason.None);

        var reason = targets.Count == 0 ? Reason.NoTaggedStorage : Reason.TaggedStorageFull;

        if (noRoom == NoRoom.ShoppingBag) left = Fill(shoppingBags, Where.ShoppingBag, left, putIn, parts);

        if (left > 0) parts.Add(new Part(Where.Waiting, left, sourceOwnerId, ""));

        return new Result(parts, reason);
    }

    /// <summary>Numbers repeated names ("Wooden Box", "Wooden Box 2") so a player can tell which of five boxes it went to.</summary>
    internal static IReadOnlyList<string> Numbered(IReadOnlyList<string> names)
    {
        var seen = new Dictionary<string, int>();

        return names
            .Select(name =>
            {
                seen[name] = seen.GetValueOrDefault(name) + 1;

                return seen[name] == 1 ? name : $"{name} {seen[name]}";
            })
            .ToList();
    }

    private static int Fill(IReadOnlyList<Place> places, Where where, int count, Func<Place, int, int> putIn, List<Part> parts)
    {
        var left = Math.Max(0, count);

        foreach (var place in places)
        {
            if (left == 0) break;

            var moved = Math.Clamp(putIn(place, left), 0, left);

            if (moved > 0) parts.Add(new Part(where, moved, place.OwnerId, place.Name));

            left -= moved;
        }

        return left;
    }
}
