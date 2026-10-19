using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>What a mod reading the Mod Manager's item settings gets back, including from a .cfg edited by hand.</summary>
public class ItemValuesTests
{
    [Fact]
    public void An_item_list_reads_its_ids_in_order_each_once()
    {
        Assert.Equal(new[] { 1203, 7, 0 }, ItemValues.ParseList(" 1203, 7,,1203 ,0"));
    }

    [Fact]
    public void A_hand_edited_item_list_keeps_the_good_ids_and_skips_the_rest()
    {
        Assert.Equal(new[] { 5, 9 }, ItemValues.ParseList("5,apple,-3,1.5,99999999999,9"));
        Assert.Empty(ItemValues.ParseList(null));
        Assert.Empty(ItemValues.ParseList(""));
    }

    [Fact]
    public void Item_counts_read_each_item_with_its_amount()
    {
        Assert.Equal(new[] { new ItemCount(1203, 5), new ItemCount(7, 2) }, ItemValues.ParseCounts("1203:5, 7 : 2"));
    }

    [Fact]
    public void Item_counts_skip_bad_entries_and_keep_the_first_amount_of_a_repeated_item()
    {
        var counts = ItemValues.ParseCounts("1203:5,7:0,8,9:x,1:2:3,-4:1,1203:9,10:1");

        Assert.Equal(new[] { new ItemCount(1203, 5), new ItemCount(10, 1) }, counts);
    }

    [Fact]
    public void Written_values_read_back_to_the_same_items()
    {
        var list = ItemValues.FormatList(new[] { 1203, 7, 1203, -1 });
        var counts = ItemValues.FormatCounts(new[] { new ItemCount(1203, 5), new ItemCount(7, 0), new ItemCount(1203, 9), new ItemCount(8, 2) });

        Assert.Equal("1203,7", list);
        Assert.Equal("1203:5,8:2", counts);
        Assert.Equal(new[] { 1203, 7 }, ItemValues.ParseList(list));
        Assert.Equal(new[] { new ItemCount(1203, 5), new ItemCount(8, 2) }, ItemValues.ParseCounts(counts));
    }
}
