using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.HotUpdate;
using GameCore.HotUpdate.Battle.Logic;
using UnityEngine;

namespace ExiledToolkit.Game;

/// <summary>
/// The game side of <see cref="TaggedPut"/>: puts an item into the home storage the player tagged for it, ranked the
/// way the game's robots rank it. A storage with no tags, or tags for other things, is never used. What no tagged
/// storage takes goes into the shopping bags by the front door or stays where it is, as the caller asks. Main thread
/// only. Every game call fails soft: a failure is logged once per kind and counts as nothing moved. Toolkit 0.31.0 and
/// newer.
/// </summary>
public sealed class TaggedStorage
{
    /// <summary>The shopping bag furniture, as the game makes it when something is bought with the bag full.</summary>
    private const int ShoppingBagFurnitureId = 1024;

    /// <summary>The game's two door boxes, shopping bags 0 and 1.</summary>
    private const int ShoppingBagCount = 2;

    private readonly Action<string> _log;
    private readonly FailureCounts _failures = new(maxLogged: 1);

    public TaggedStorage(Action<string> log) => _log = log;

    private sealed record Scene(BattleLogicWorld World, AgentManager Agents, ItemManager Items, ConfigManager Config);

    /// <summary>
    /// The home storages tagged for the item, best first, for showing where it would go. Room is not checked: the game
    /// can only check room for an item that already exists, so Move and Add find out by trying. Empty when the game
    /// isn't ready.
    /// </summary>
    public IReadOnlyList<TaggedPut.Place> Rank(int itemConfigId)
    {
        try
        {
            var game = Ready();
            var row = game == null ? null : ItemRow(game.Config, itemConfigId);

            if (game == null || row == null) return Array.Empty<TaggedPut.Place>();

            return TaggedPut.Targets(Candidates(game, row, NewItemDaysLeft(row)));
        }
        catch (Exception ex)
        {
            Report("rank storages", ex);

            return Array.Empty<TaggedPut.Place>();
        }
    }

    /// <summary>
    /// Moves a stack that already exists, all of it, from where it is into tagged storage, keeping its freshness and
    /// uses. What doesn't fit goes to the shopping bags or stays at fromOwnerId, as noRoom says.
    /// </summary>
    public TaggedPut.Result Move(long itemInstanceId, long fromOwnerId, TaggedPut.NoRoom noRoom)
    {
        try
        {
            var game = Ready();
            var item = game?.Items.GetItemData(itemInstanceId);
            var count = CountAt(item, itemInstanceId, fromOwnerId);
            var row = game == null || item == null ? null : ItemRow(game.Config, item.ItemConfigId);

            if (game == null || item == null || row == null || count == 0) return TaggedPut.Result.NotReady(count, fromOwnerId);

            var configId = item.ItemConfigId;
            var daysLeft = ItemManager.GetRemainingShelfDays(row, item, RobotSortingManager.GetNowHours(game.World));

            return TaggedPut.Put(Candidates(game, row, daysLeft), ShoppingBags(game, noRoom), count, noRoom, fromOwnerId, (place, asked) =>
                IsShoppingBag(place) ? MoveIntoBag(game, itemInstanceId, fromOwnerId, configId, place.OwnerId, asked) : MoveIntoStorage(game, itemInstanceId, fromOwnerId, place.OwnerId));
        }
        catch (Exception ex)
        {
            Report("move into tagged storage", ex);

            return TaggedPut.Result.NotReady(0, fromOwnerId);
        }
    }

    /// <summary>
    /// Makes count new items straight in tagged storage, fresh as the game makes them. What doesn't fit goes to the
    /// shopping bags, and what is still left (a Waiting part, owner 0) was never made. A yield that must not be lost
    /// goes into a bag first, then through Move.
    /// </summary>
    public TaggedPut.Result Add(int itemConfigId, int count, TaggedPut.NoRoom noRoom)
    {
        try
        {
            var game = Ready();
            var row = game == null ? null : ItemRow(game.Config, itemConfigId);

            if (game == null || row == null) return TaggedPut.Result.NotReady(count, 0);

            return TaggedPut.Put(Candidates(game, row, NewItemDaysLeft(row)), ShoppingBags(game, noRoom), count, noRoom, 0, (place, asked) =>
                AddInto(game, row, place, asked));
        }
        catch (Exception ex)
        {
            Report("add into tagged storage", ex);

            return TaggedPut.Result.NotReady(count, 0);
        }
    }

    private static Scene? Ready()
    {
        var world = GameSingleton.Existing<BattleLogicWorld>();
        var config = GameSingleton.Existing<ConfigManager>();
        var agents = world?._AgentManager;
        var items = world?._ItemManager;

        return world == null || config == null || agents == null || items == null ? null : new Scene(world, agents, items, config);
    }

    /// <summary>
    /// Every home storage Move and Add can pick, named the way they report it, with its tags and how full it is. Empty
    /// when the game isn't ready.
    /// </summary>
    public IReadOnlyList<TaggedPut.Storage> Storages()
    {
        try
        {
            var game = Ready();

            if (game == null) return Array.Empty<TaggedPut.Storage>();

            return HomeStorages(game).Select(home => StorageOf(home.Furniture, home.Name)).ToList();
        }
        catch (Exception ex)
        {
            Report("list storages", ex);

            return Array.Empty<TaggedPut.Storage>();
        }
    }

    /// <summary>
    /// The stacks in the two shopping bags by the front door, each with the storage tags that would take it and whether
    /// a home storage is tagged for it now. Empty when the game isn't ready.
    /// </summary>
    public IReadOnlyList<TaggedPut.BagStack> ShoppingBagStacks()
    {
        try
        {
            var game = Ready();

            if (game == null) return Array.Empty<TaggedPut.BagStack>();

            var homes = HomeStorages(game);
            var tagIds = TagOptions();
            var nowHours = RobotSortingManager.GetNowHours(game.World);
            var stacks = new List<TaggedPut.BagStack>();

            for (var box = 0; box < ShoppingBagCount; box++) AddBagStacks(game, box, homes, tagIds, nowHours, stacks);

            return stacks;
        }
        catch (Exception ex)
        {
            Report("read the shopping bags", ex);

            return Array.Empty<TaggedPut.BagStack>();
        }
    }

    /// <summary>
    /// The storage tags that would take a fresh one of an item, in the order the game's tag picker lists them. Needs
    /// Cold goes by how many days a stack has left, so a fresh one only gets it when it keeps under 10 days. Empty for
    /// an unknown item and while the game's tables aren't loaded.
    /// </summary>
    public IReadOnlyList<int> TagsOf(int itemConfigId)
    {
        try
        {
            if (GameSingleton.Existing<ConfigManager>() is not { } config || ItemRow(config, itemConfigId) is not { } row) return Array.Empty<int>();

            return TagOptions().Where(tagId => FurnitureTagRules.OptionMatches(tagId, row, NewItemDaysLeft(row))).ToList();
        }
        catch (Exception ex)
        {
            Report("read an item's tags", ex);

            return Array.Empty<int>();
        }
    }

    /// <summary>
    /// A storage tag's name in the game's language, as its tag picker shows it. "" for an unknown tag, and while the
    /// game's tables aren't loaded.
    /// </summary>
    public string TagName(int tagId)
    {
        try
        {
            return GameNames.NonEmpty(FurnitureTagRules.GetOptionName(tagId)) ?? "";
        }
        catch (Exception ex)
        {
            Report("read a tag name", ex);

            return "";
        }
    }

    /// <summary>
    /// Every home storage with its rank for the item, null where its tags don't want it. Names are numbered over all of
    /// them, so "Wooden Box 2" is the same box whatever is put away.
    /// </summary>
    private static List<TaggedPut.Place> Candidates(Scene game, Config_Item row, float daysLeft) =>
        HomeStorages(game).Select(home => new TaggedPut.Place(home.Furniture.InstanceId, home.Name, RankOf(game.Config, home.Furniture, row, daysLeft))).ToList();

    /// <summary>The home storages Move and Add can use, in the game's order, with their numbered names.</summary>
    private static List<(Furniture Furniture, string Name)> HomeStorages(Scene game)
    {
        var found = game.Agents.GetFurnituresWithBag(game.Agents.GetHomeMapId(), true, true);
        var basketId = NeighborRules.GetBasketFurnitureId();
        var storages = new List<Furniture>();

        for (var i = 0; found != null && i < found.Count; i++)
        {
            var furniture = found[i];

            if (furniture != null && IsPlayerStorage(furniture, basketId)) storages.Add(furniture);
        }

        var names = TaggedPut.Numbered(storages.Select(furniture => FurnitureName(game.Config, furniture.AgentConfigId)).ToList());

        return storages.Select((furniture, i) => (furniture, names[i])).ToList();
    }

    /// <summary>The storage's tag list and how full it is, the same count the game's label over the storage shows (Furniture.CountBagCells).</summary>
    private static TaggedPut.Storage StorageOf(Furniture furniture, string name)
    {
        var rule = furniture.GetAgentComponent<FurnitureTagComponent>()?.TagIds;
        var tags = new List<int>();

        for (var i = 0; rule != null && i < rule.Count; i++) tags.Add(rule[i]);

        furniture.CountBagCells(out var used, out var total);

        return new TaggedPut.Storage(furniture.InstanceId, furniture.AgentConfigId, name, tags, used, total);
    }

    /// <summary>One shopping bag's stacks. A recycled stack can sit in a list it left, so its owner is checked.</summary>
    private static void AddBagStacks(Scene game, int box, List<(Furniture Furniture, string Name)> homes, IReadOnlyList<int> tagIds, float nowHours, List<TaggedPut.BagStack> into)
    {
        var bagOwnerId = VehicleConfig.GetBoxOwnerId(box);
        var held = game.Items.GetItemDataList(bagOwnerId);

        for (var i = 0; held != null && i < held.Count; i++)
        {
            var item = held[i];
            var row = item == null || item.OwnerId != bagOwnerId ? null : ItemRow(game.Config, item.ItemConfigId);

            if (item == null || row == null || item.ItemCount <= 0) continue;

            var daysLeft = ItemManager.GetRemainingShelfDays(row, item, nowHours);
            var tags = tagIds.Where(tagId => FurnitureTagRules.OptionMatches(tagId, row, daysLeft)).ToList();
            var hasStorage = homes.Any(home => RankOf(game.Config, home.Furniture, row, daysLeft) != null);

            into.Add(new TaggedPut.BagStack(box + 1, bagOwnerId, item.InstanceId, item.ItemConfigId, item.ItemCount, tags, hasStorage));
        }
    }

    /// <summary>The tags a player can pick in the game's tag picker, in its order. Empty when its tables aren't loaded.</summary>
    private static IReadOnlyList<int> TagOptions()
    {
        var options = FurnitureTagRules.GetVisibleOptions()?.TryCast<Il2CppSystem.Collections.Generic.List<Config_FurnitureTag>>();
        var ids = new List<int>();

        for (var i = 0; options != null && i < options.Count; i++)
        {
            if (options[i] != null) ids.Add(options[i].ID);
        }

        return ids;
    }

    /// <summary>
    /// Left out: the neighbor's basket (what sits in it is for her), storage the game leaves out when it counts items,
    /// the drone bay (a trade takes its goods), and the shopping bags, which are only the overflow.
    /// </summary>
    private static bool IsPlayerStorage(Furniture furniture, int basketId)
    {
        var configId = furniture.AgentConfigId;

        if (basketId != 0 && configId == basketId) return false;

        if (configId == ShoppingBagFurnitureId || VehicleConfig.IsBoxOwner(furniture.InstanceId)) return false;

        if (ItemManager.IsExcludedFromCostPool(furniture.InstanceId)) return false;

        return !StrangerTradeActiveManager.IsDroneHub(configId) && StrangerTradeActiveManager.TripOf(furniture) == null;
    }

    /// <summary>
    /// The rank the game's robots use: a material rack by its slots (and only while its robot put switch is on), any
    /// other storage by its tags. An empty tag list never ranks (FurnitureTagRules.Evaluate).
    /// </summary>
    private static TaggedPut.Rank? RankOf(ConfigManager config, Furniture furniture, Config_Item row, float daysLeft)
    {
        var configId = furniture.AgentConfigId;
        int level, specificity;

        if (config.IsMaterialRackFurniture(configId))
        {
            var rack = furniture.GetAgentComponent<MaterialRackComponent>();

            if (rack == null || !rack.GetAllowRobotPut()) return null;

            return rack.TryGetPutRank(configId, row, daysLeft, out level, out specificity) ? new TaggedPut.Rank(level, specificity) : null;
        }

        var tags = furniture.GetAgentComponent<FurnitureTagComponent>();

        return tags != null && tags.TryGetPutRank(configId, row, daysLeft, out level, out specificity) ? new TaggedPut.Rank(level, specificity) : null;
    }

    /// <summary>
    /// The two shopping bags, only at home (they sit by the front door) and only when the caller wants them. A bag the
    /// game hasn't made yet is made when something goes into it.
    /// </summary>
    private static IReadOnlyList<TaggedPut.Place> ShoppingBags(Scene game, TaggedPut.NoRoom noRoom)
    {
        if (noRoom != TaggedPut.NoRoom.ShoppingBag || !game.Agents.IsHomeMap(ExploreSearchStateManager.GetCurrentMapId())) return Array.Empty<TaggedPut.Place>();

        var name = FurnitureName(game.Config, ShoppingBagFurnitureId);
        var names = TaggedPut.Numbered(Enumerable.Repeat(name, ShoppingBagCount).ToList());

        return Enumerable.Range(0, ShoppingBagCount).Select(box => new TaggedPut.Place(VehicleConfig.GetBoxOwnerId(box), names[box])).ToList();
    }

    private static bool IsShoppingBag(TaggedPut.Place place) => VehicleConfig.IsBoxOwner(place.OwnerId);

    /// <summary>
    /// The game's quick move, packed the way its robots pack (away from mold). It moves what fits and leaves the rest,
    /// so what moved is read from the stack, whatever it returns.
    /// </summary>
    private int MoveIntoStorage(Scene game, long itemId, long fromOwnerId, long toOwnerId)
    {
        try
        {
            if (toOwnerId == fromOwnerId) return 0;

            var before = CountAt(game.Items.GetItemData(itemId), itemId, fromOwnerId);

            if (before == 0) return 0;

            game.Items.TryQuickMoveItem(itemId, fromOwnerId, toOwnerId, true, true);

            return before - CountAt(game.Items.GetItemData(itemId), itemId, fromOwnerId);
        }
        catch (Exception ex)
        {
            Report("move into a storage", ex);

            return 0;
        }
    }

    /// <summary>
    /// The game refuses to quick move into a shopping bag, so a copy with the same freshness and uses is made in the
    /// bag, the way Unpack All fills them, and what the bag took is then taken from the source.
    /// </summary>
    private int MoveIntoBag(Scene game, long itemId, long fromOwnerId, int configId, long boxOwnerId, int asked)
    {
        try
        {
            var item = game.Items.GetItemData(itemId);
            var before = CountAt(item, itemId, fromOwnerId);
            var count = Math.Min(asked, before);

            if (item == null || count == 0 || !EnsureShoppingBag(game, boxOwnerId)) return 0;

            var held = HeldCount(game.Items, boxOwnerId, configId);

            game.Items.TryAddItemToBag(boxOwnerId, configId, count, item.StartTime, item.TimeLeft, item.UseTimes, item.TimeScale, null, item.MaxUseTimes, item.InstanceVD, item.InstanceEffectEnd, item.OriginalTimeLeft, item.InstanceBurnValue, item.Polluted, item.IsMapPreset, item.NoPackage, true, item.InstanceWeight);

            var moved = Math.Max(0, HeldCount(game.Items, boxOwnerId, configId) - held);

            if (moved == 0) return 0;

            game.Items.CostItem(fromOwnerId, itemId, moved, true);
            game.Agents.GetLeadingRole()?.PushDoorBoxToRedux();

            var taken = before - CountAt(game.Items.GetItemData(itemId), itemId, fromOwnerId);

            if (taken < moved) _log($"tagged storage: {moved} of item {configId} went into a shopping bag but only {taken} left {fromOwnerId}");

            return moved;
        }
        catch (Exception ex)
        {
            Report("move into a shopping bag", ex);

            return 0;
        }
    }

    /// <summary>
    /// New items with the clock AE_AddItemInBag gives them: made this hour, with the item's full life. TryAddItemToBag
    /// checks room and adds what fits, so what went in is read from the bag.
    /// </summary>
    private int AddInto(Scene game, Config_Item row, TaggedPut.Place place, int count)
    {
        try
        {
            var isBag = IsShoppingBag(place);

            if (isBag && !EnsureShoppingBag(game, place.OwnerId)) return 0;

            var held = HeldCount(game.Items, place.OwnerId, row.ID);
            var timeLeft = row.Life <= 0 ? -1 : 24 * row.Life;

            game.Items.TryAddItemToBag(place.OwnerId, row.ID, count, game.World._GameTimeManager.GetHour(), timeLeft, 0, 1f, null, 0, null, null, 0, 0, false, false, false, true, 0);

            var added = Math.Max(0, HeldCount(game.Items, place.OwnerId, row.ID) - held);

            if (isBag && added > 0) game.Agents.GetLeadingRole()?.PushDoorBoxToRedux();

            return added;
        }
        catch (Exception ex)
        {
            Report("add into a storage", ex);

            return 0;
        }
    }

    /// <summary>Makes the shopping bag first when the game hasn't yet, the way Unpack All and Pack Rat do.</summary>
    private static bool EnsureShoppingBag(Scene game, long boxOwnerId)
    {
        var box = Enumerable.Range(0, ShoppingBagCount).FirstOrDefault(index => VehicleConfig.GetBoxOwnerId(index) == boxOwnerId, -1);

        if (box < 0) return false;

        if (game.Agents.FindDoorBoxFurniture(box) != null) return true;

        var role = game.Agents.GetLeadingRole();
        var build = role == null ? null : AgentTools.GetAgentComponent<BuildComponent>(role);
        var position = role == null ? Vector3.zero : AgentTools.GetAgentComponent<TransformComponent>(role)?.Position ?? Vector3.zero;

        return build != null && build.CreateBagFurniture(ShoppingBagFurnitureId, position, true, box) != 0;
    }

    /// <summary>The stack's count where it should be, 0 when it has moved on or the game recycled it.</summary>
    private static int CountAt(ItemData? item, long itemId, long ownerId) =>
        item != null && item.InstanceId == itemId && item.OwnerId == ownerId ? Math.Max(0, item.ItemCount) : 0;

    /// <summary>How many of the item the owner holds over all its stacks. A recycled stack can sit in a list it left, so its owner is checked.</summary>
    private static int HeldCount(ItemManager items, long ownerId, int configId)
    {
        var held = items.GetItemDataList(ownerId);
        var total = 0;

        for (var i = 0; held != null && i < held.Count; i++)
        {
            var item = held[i];

            if (item != null && item.OwnerId == ownerId && item.ItemConfigId == configId) total += Math.Max(0, item.ItemCount);
        }

        return total;
    }

    /// <summary>A new item's days left, the way GetRemainingShelfDays reads an item that never spoils.</summary>
    private static float NewItemDaysLeft(Config_Item row) => row.Life > 0 ? row.Life : float.MaxValue;

    private static Config_Item? ItemRow(ConfigManager config, int configId) =>
        config._Config_Item_Dict is { } table && table.TryGetValue(configId, out var row) ? row : null;

    private static string FurnitureName(ConfigManager config, int configId)
    {
        var key = config._Config_Furniture_Dict is { } table && table.TryGetValue(configId, out var row) ? row?.Name : null;

        return GameNames.Translate(config, key) ?? GameNames.NonEmpty(key) ?? (GameLanguage.IsChinese() ? "储物" : "Storage");
    }

    private void Report(string doing, Exception ex)
    {
        var line = _failures.LineFor($"tagged storage: {doing}", ex);

        if (line != null) _log(line);
    }
}
