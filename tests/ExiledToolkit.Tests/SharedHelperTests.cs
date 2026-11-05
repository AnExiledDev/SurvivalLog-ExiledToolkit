using ExiledToolkit.Core;
using ExiledToolkit.Game;
using ExiledToolkit.Kit;
using ExiledToolkit.Web;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>The rules behind the shared game-facing helpers, without the game.</summary>
public class SharedHelperTests
{
    [Fact]
    public void A_text_literal_cannot_break_out_of_the_script_and_matches_the_toasts_own()
    {
        var text = "a \"b\" \\ </script>\u2028\n";

        Assert.Equal("\"a \\\"b\\\" \\\\ \\u003c/script>\\u2028\\u000a\"", JsText.Literal(text));
        Assert.Equal(ToastScript.Str(text), JsText.Literal(text));
        Assert.Equal("\"\"", JsText.Literal(null));
    }

    [Fact]
    public void An_icon_named_by_a_table_is_found_by_its_file_name_whatever_path_the_table_gives()
    {
        var icons = IconIndex.Build(new[] { "Res\\Furniture\\UI_Item_Icon_Furnit_M_8313.png", "Res/Food/UI_Item_Icon_Food_1.png" });

        Assert.Equal("Res/Furniture/UI_Item_Icon_Furnit_M_8313.png", icons.Resolve("../../Res/Furniture/UI_Item_Icon_Furnit_M_8313.png"));
        Assert.Equal("Res/Food/UI_Item_Icon_Food_1.png", icons.Resolve("ui_item_icon_food_1"));
        Assert.Null(icons.Resolve("UI_Item_Icon_Missing"));
        Assert.Null(icons.Resolve(" "));
        Assert.Null(IconIndex.Empty.Resolve("UI_Item_Icon_Food_1"));
    }

    [Fact]
    public void Of_two_icons_with_one_name_the_one_whose_folder_the_table_names_wins()
    {
        var icons = IconIndex.Build(new[] { "Res/Food/Same.png", "Res/Tool/Same.png" });

        Assert.Equal("Res/Tool/Same.png", icons.Resolve("../../Res/Tool/Same.png"));
        Assert.Equal("Res/Food/Same.png", icons.Resolve("Same"));
    }

    [Fact]
    public void An_item_is_named_by_its_translation_then_its_local_name_then_its_key()
    {
        Assert.Equal("Wood", GameNames.FirstName("Wood", "木头", "item_wood"));
        Assert.Equal("木头", GameNames.FirstName(null, "木头", "item_wood"));
        Assert.Equal("item_wood", GameNames.FirstName(null, " ", "item_wood"));
        Assert.Null(GameNames.FirstName(null, "", null));
    }

    [Fact]
    public void A_key_the_language_table_hands_back_or_leaves_blank_has_no_translation()
    {
        Assert.Null(GameNames.Translated("item_wood", "item_wood"));
        Assert.Null(GameNames.Translated("item_wood", ""));
        Assert.Null(GameNames.Translated("item_wood", null));
        Assert.Equal("Wood", GameNames.Translated("item_wood", "Wood"));
        Assert.Null(GameNames.NonEmpty("  "));
    }

    [Fact]
    public void Held_hotkeys_stay_off_through_the_frame_after_the_last_holder_lets_go()
    {
        var frame = 10;
        var holds = new KeyHolds();

        holds.Hold("panel");
        holds.Hold("menu");
        Assert.False(holds.LetsGameRun(() => frame));

        holds.Release("panel", () => frame);
        Assert.False(holds.LetsGameRun(() => frame));

        holds.Release("menu", () => frame);
        frame = 11;
        Assert.False(holds.LetsGameRun(() => frame));

        frame = 12;
        Assert.True(holds.LetsGameRun(() => frame));
    }

    [Fact]
    public void Letting_go_of_a_hold_never_taken_changes_nothing_and_reads_no_frame()
    {
        var holds = new KeyHolds();

        holds.Release("never held", () => throw new InvalidOperationException("the frame was read"));

        Assert.True(holds.LetsGameRun(() => throw new InvalidOperationException("the frame was read")));
        Assert.False(holds.IsHeld);
    }

    [Fact]
    public void Each_kind_of_hook_failure_is_logged_only_up_to_its_limit()
    {
        var once = new FailureCounts(1);
        var thrice = new FailureCounts(3);
        var ex = new InvalidOperationException("boom");

        Assert.StartsWith("failed reading a crop (reported once): ", once.LineFor("reading a crop", ex));
        Assert.Null(once.LineFor("reading a crop", ex));
        Assert.NotNull(once.LineFor("reading a pot", ex));

        Assert.StartsWith("failed x (logged 1 of 3)", thrice.LineFor("x", ex));
        Assert.StartsWith("failed x (logged 3 of 3)", (thrice.LineFor("x", ex), thrice.LineFor("x", ex)).Item2);
        Assert.Null(thrice.LineFor("x", ex));
    }

    [Fact]
    public void A_mods_ui_size_setting_names_the_mod_in_both_languages()
    {
        Assert.Equal("Farm Hub", UiSize.DisplayName("FarmHub"));
        Assert.Equal("Performance Analyzer", UiSize.DisplayName("PerformanceAnalyzer"));
        Assert.Equal("Stacking", UiSize.DisplayName("Stacking"));

        var description = UiSize.ModDescription("Farm Hub");

        Assert.StartsWith("How big Farm Hub's own windows, panels and messages are drawn, in percent, from 75 to 200. At 0 they follow the UI size set in Exiled's Toolkit (its UiScale).\n", description);
        Assert.EndsWith("\nFarm Hub 自己的窗口、面板和提示的显示大小（百分比），范围 75 到 200。设为 0 时跟随 Exiled's Toolkit 的界面大小设置（它的 UiScale）。", description);
    }
}
