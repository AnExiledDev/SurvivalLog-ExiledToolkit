using System.Globalization;
using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>The toast stack toasts.js draws in the game's web UI, fed ops the way the plugin sends them.</summary>
public class PageScriptTests
{
    private const string Stamina = "Not enough stamina";

    private static ToastView View(long id, string title, int count = 1, int milliseconds = 7_000, bool hasClick = false) =>
        new(new ToastId(id), new ToastContent("Survival Log", title, null, ToastKind.Warning, null), count, milliseconds, hasClick);

    private static PageCommand.Gone TimedOut(long id, int count) => new(new ToastId(id), count, false);

    private static PageCommand.Gone Closed(long id, int count) => new(new ToastId(id), count, true);

    private static List<PageCommand.Gone> GoneSent(PageScript page) => page.Sent.OfType<PageCommand.Gone>().ToList();

    // ===== Drawing =====

    [Fact]
    public void New_toasts_stack_newest_on_top_and_a_merge_shows_its_count_on_the_same_box()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)), new PageOp.Show(View(2, "Saved")));
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 3)));

        Assert.Equal(new[] { "Saved", Stamina + " x3" }, page.Labels());
        Assert.Equal(2, page.BoxCount());
    }

    [Fact]
    public void A_replaced_toast_takes_the_old_box_in_its_place_and_answers_to_its_new_id()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, "Organizing 1/3")), new PageOp.Show(View(2, "Unrelated")));
        page.Apply(new PageOp.Replace(new ToastId(1), View(3, "Organizing 2/3", hasClick: true)));
        var labels = page.Labels();
        page.Click(1);

        Assert.Equal(new[] { "Unrelated", "Organizing 2/3" }, labels);
        Assert.Contains(new PageCommand.Click(new ToastId(3)), page.Sent);
        Assert.Equal(new[] { Closed(3, 1) }, GoneSent(page));
    }

    [Fact]
    public void A_removed_box_reports_gone_and_leaves_the_stack_after_its_exit()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Apply(new PageOp.Remove(new ToastId(1)));
        var leaving = page.BoxCount();
        page.Advance(450);

        Assert.Equal(new[] { TimedOut(1, 1) }, GoneSent(page));
        Assert.Empty(page.Labels());
        Assert.Equal(1, leaving);
        Assert.Equal(0, page.BoxCount());
    }

    [Fact]
    public void Closing_a_toast_with_its_x_reports_it_gone_without_running_its_click_action()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina, hasClick: true)));
        page.Click(0, "etk-close");

        Assert.Empty(page.Sent.OfType<PageCommand.Click>());
        Assert.Equal(new[] { Closed(1, 1) }, GoneSent(page));
    }

    /// <summary>A merge the plugin sent before it heard of the close must not bring the box back.</summary>
    [Fact]
    public void A_merge_for_a_box_the_player_closed_is_not_drawn_again()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Click(0, "etk-close");
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 2)));
        page.Advance(450);

        Assert.Empty(page.Labels());
        Assert.Equal(0, page.BoxCount());
        Assert.Equal(new[] { Closed(1, 1) }, GoneSent(page));
    }

    // ===== Time =====

    [Fact]
    public void A_box_that_runs_out_of_time_reports_gone_with_the_count_it_showed()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Advance(3_000);
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 4)));
        page.Advance(6_999);
        var early = GoneSent(page);
        page.Advance(1);

        Assert.Empty(early);
        Assert.Equal(new[] { TimedOut(1, 4) }, GoneSent(page));
    }

    /// <summary>The "not enough stamina" spam case: one more trigger lands just as the box times out.</summary>
    [Fact]
    public void A_merge_for_a_box_on_its_way_out_draws_it_again_with_the_new_count()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Advance(7_000);
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 2)));
        page.Advance(450);
        var labels = page.Labels();
        var count = page.BoxCount();
        page.Advance(7_000);

        Assert.Equal(new[] { Stamina + " x2" }, labels);
        Assert.Equal(1, count);
        Assert.Equal(new[] { TimedOut(1, 1), TimedOut(1, 2) }, GoneSent(page));
    }

    [Fact]
    public void A_merge_for_a_box_the_page_does_not_have_draws_it()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Merge(View(5, Stamina, count: 6)));

        Assert.Equal(new[] { Stamina + " x6" }, page.Labels());
    }

    [Fact]
    public void A_burst_of_merges_starts_the_bump_animation_once_until_it_has_run()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 2)));
        page.Advance(100);
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 3)));
        var duringBump = page.Number("boxes()[0].added['etk-bump']");
        page.Advance(160);
        page.Apply(new PageOp.Merge(View(1, Stamina, count: 4)));

        Assert.Equal(1, duringBump);
        Assert.Equal(2, page.Number("boxes()[0].added['etk-bump']"));
        Assert.Equal(new[] { Stamina + " x4" }, page.Labels());
    }

    // ===== Talking to the plugin =====

    [Fact]
    public void With_the_bridge_already_there_the_page_says_ready_at_once()
    {
        var page = new PageScript();

        Assert.IsType<PageCommand.Ready>(Assert.Single(page.Sent));
    }

    [Fact]
    public void The_ready_message_waits_for_a_bridge_that_arrives_after_the_script()
    {
        var page = new PageScript(hasBridge: false);
        var beforeBridge = page.Sent.Count;

        page.ConnectBridge();

        Assert.Equal(0, beforeBridge);
        Assert.IsType<PageCommand.Ready>(Assert.Single(page.Sent));
    }

    /// <summary>With no HUD measured yet, the box sits where the HUD's tool column and queue are at 1920 x 1080.</summary>
    [Fact]
    public void While_boxes_are_up_the_page_repeats_where_they_are_every_second()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Sent.Clear();
        page.Advance(999);
        var beforeBeat = page.Sent.Count;
        page.Advance(1);
        var rects = Assert.IsType<PageCommand.Rects>(Assert.Single(page.Sent));
        var box = Px(Assert.Single(rects.Boxes));

        Assert.Equal(0, beforeBeat);
        Assert.Equal((88, 920, 280, 60), (box.Left, box.Top, box.Width, box.Height));
    }

    [Fact]
    public void With_no_boxes_up_the_page_stays_quiet()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina)));
        page.Apply(new PageOp.Remove(new ToastId(1)));
        page.Advance(450);
        var cleared = Assert.IsType<PageCommand.Rects>(page.Sent[^1]);
        page.Sent.Clear();
        page.Advance(5_000);

        Assert.Empty(cleared.Boxes);
        Assert.Empty(page.Sent);
    }

    // ===== Beside the HUD =====

    [Fact]
    public void Beside_the_hud_the_deck_sits_right_of_the_tool_column_and_above_the_action_panel()
    {
        var page = new PageScript();
        page.Run("addHud(1)");

        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));
        var box = Px(Assert.Single(LastRects(page).Boxes));

        Assert.Equal(77 + 10, box.Left);
        Assert.Equal(980 - 8, box.Bottom);
    }

    [Fact]
    public void In_the_prologue_with_no_action_panel_the_deck_lines_up_with_the_foot_of_the_tool_column()
    {
        var page = new PageScript();
        page.Run("setPart(addFrame('CoreUI0', 'https://webui/UI/CoreUI0/CoreUI0.html', 1), '.vertical-tools', 30, 700, 102, 1050)");

        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));
        var box = Px(Assert.Single(LastRects(page).Boxes));

        Assert.Equal(102 + 10, box.Left);
        Assert.Equal(1050, box.Bottom);
    }

    /// <summary>The prologue's checklist bubble opens right of the column; the deck must not cover the next task.</summary>
    [Fact]
    public void While_the_prologue_checklist_bubble_shows_the_deck_sits_right_of_it_and_moves_back_when_it_goes()
    {
        var page = new PageScript();
        page.Run("var prologue = addFrame('CoreUI0', 'https://webui/UI/CoreUI0/CoreUI0.html', 1); setPart(prologue, '.vertical-tools', 30, 700, 102, 1050); setPart(prologue, '.checklist-bubble', 116, 900, 340, 960)");

        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));
        var withBubble = Px(Assert.Single(LastRects(page).Boxes));
        page.Run("setPart(prologue, '.checklist-bubble', 0, 0, 0, 0)");
        page.Advance(250);
        var withoutBubble = Px(Assert.Single(LastRects(page).Boxes));

        Assert.Equal(340 + 10, withBubble.Left);
        Assert.Equal(102 + 10, withoutBubble.Left);
    }

    /// <summary>In plan mode the game hides the tool column and the queue; the deck must not jump to the fallback.</summary>
    [Fact]
    public void While_the_hud_is_hidden_the_deck_stays_put_and_it_follows_the_hud_when_it_moves()
    {
        var page = new PageScript();
        page.Run("var hud = addHud(1)");

        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));
        page.Run("setPart(hud, '.vertical-tools', 0, 0, 0, 0); setPart(hud, '.action-panel', 0, 0, 0, 0)");
        page.Advance(250);
        var whileHidden = Px(Assert.Single(LastRects(page).Boxes));
        page.Run("setPart(hud, '.vertical-tools', 18, 600, 120, 963); setPart(hud, '.action-panel', 18, 940, 900, 1062)");
        page.Advance(250);
        var moved = Px(Assert.Single(LastRects(page).Boxes));

        Assert.Equal((87, 972), (whileHidden.Left, whileHidden.Bottom));
        Assert.Equal((130, 932), (moved.Left, moved.Bottom));
    }

    [Theory]
    [InlineData(ToastPosition.TopLeft, 16, 16)]
    [InlineData(ToastPosition.TopRight, 1920 - 16 - 280, 16)]
    [InlineData(ToastPosition.BottomLeft, 16, 1080 - 16 - 60)]
    [InlineData(ToastPosition.BottomRight, 1920 - 16 - 280, 1080 - 16 - 60)]
    public void A_corner_setting_puts_the_deck_in_that_corner_even_with_the_hud_up(ToastPosition corner, int left, int top)
    {
        var page = new PageScript();
        page.Run("addHud(1)");

        page.Apply(new PageOp.Place(corner), new PageOp.Show(View(1, Stamina)));
        var box = Px(Assert.Single(LastRects(page).Boxes));

        Assert.Equal((left, top), (box.Left, box.Top));
    }

    // ===== At another UI size =====

    /// <summary>Interface &gt; UiScale grows the deck at once, still beside the HUD, and clicks still land on its cards.</summary>
    [Fact]
    public void A_bigger_ui_size_grows_the_deck_at_once_in_its_place_beside_the_hud()
    {
        var page = new PageScript(uiPercent: 100);
        page.Run("addHud(1)");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));

        page.Run(UiSize.SetScript(150));
        page.NextFrame();
        var box = Px(Assert.Single(LastRects(page).Boxes));

        Assert.Equal((77 + 10, 980 - 8), (box.Left, box.Bottom));
        Assert.Equal((280 * 3 / 2, 60 * 3 / 2), (box.Width, box.Height));
    }

    [Fact]
    public void A_bigger_deck_in_a_corner_keeps_its_gap_from_the_screen_edges()
    {
        var page = new PageScript(uiPercent: 200);
        page.NextFrame();

        page.Apply(new PageOp.Place(ToastPosition.BottomRight), new PageOp.Show(View(1, Stamina)));
        var box = Px(Assert.Single(LastRects(page).Boxes));

        Assert.Equal((1920 - 16, 1080 - 16), (box.Left + box.Width, box.Bottom));
    }

    /// <summary>The bigger deck covers more of the screen, so it lets clicks through to a panel button it now reaches.</summary>
    [Theory]
    [InlineData(100, false)]
    [InlineData(200, true)]
    public void A_panel_button_near_the_deck_gets_its_clicks_once_the_ui_size_makes_the_deck_reach_it(int percent, bool isThrough)
    {
        var page = new PageScript(uiPercent: percent);
        page.Run("addHud(1)");
        page.Run("addFrame('BattleBag', 'https://webui/UI/BattleBag/BattleBag.html', 4)");
        page.NextFrame();
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));

        page.Run("window.syncInteractiveRects('BattleBag', [{ x: 0.25, y: 0.86, w: 0.1, h: 0.02 }], null)");
        page.NextFrame();

        Assert.Equal(isThrough ? 0 : 1, LastRects(page).Boxes.Count);
        Assert.Equal("false", page.Text("stack().classList.contains('etk-hidden')"));
    }

    // ===== The deck =====

    /// <summary>A card past the fourth would sit unseen behind the last, so it is not drawn and takes no clicks.</summary>
    [Fact]
    public void The_newest_toast_is_in_front_and_up_to_three_older_ones_peek_out_narrower_behind_it()
    {
        var page = new PageScript();
        page.Run("addHud(1)");

        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, "1")), new PageOp.Show(View(2, "2")), new PageOp.Show(View(3, "3")), new PageOp.Show(View(4, "4")), new PageOp.Show(View(5, "5")));
        var boxes = LastRects(page).Boxes.Select(Px).ToList();

        Assert.Equal(new[] { "5", "4", "3", "2", "1" }, page.Labels());
        Assert.Equal(new[] { 87, 93, 99, 105 }, boxes.Select(box => box.Left));
        Assert.Equal(new[] { 280, 268, 256, 244 }, boxes.Select(box => box.Width));
        Assert.Equal(new[] { 972, 964, 956, 948 }, boxes.Select(box => box.Bottom));
        Assert.All(boxes, box => Assert.Equal(60, box.Height));
        Assert.Equal(",,,,hidden", page.Text("boxes().map(function (box) { return box.style.visibility; }).join(',')"));
    }

    [Fact]
    public void Hovering_an_older_toast_brings_it_to_the_front_in_full_and_leaving_it_puts_it_back()
    {
        var page = new PageScript();
        page.Run("addHud(1)");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, "1")), new PageOp.Show(View(2, "2")), new PageOp.Show(View(3, "3")));

        page.Run("dispatch(boxes()[2], 'mouseenter')");
        var hovered = Px(LastRects(page).Boxes[2]);
        var isOnTop = page.Text("boxes()[2].classList.contains('etk-front') && Number(boxes()[2].style.zIndex) > Number(boxes()[0].style.zIndex)");
        page.Run("dispatch(boxes()[2], 'mouseleave')");
        var back = Px(LastRects(page).Boxes[2]);

        Assert.Equal("true", isOnTop);
        Assert.Equal((87, 280, 956, 84), (hovered.Left, hovered.Width, hovered.Bottom, hovered.Height));
        Assert.Equal((99, 256, 956, 60), (back.Left, back.Width, back.Bottom, back.Height));
        Assert.Equal("false", page.Text("boxes()[2].classList.contains('etk-front')"));
    }

    [Fact]
    public void A_hovered_toast_waits_while_the_rest_run_out_and_hovering_another_lets_the_first_go()
    {
        var page = new PageScript();
        page.Apply(new PageOp.Show(View(1, "1")), new PageOp.Show(View(2, "2")), new PageOp.Show(View(3, "3")));

        page.Run("dispatch(boxes()[2], 'mouseenter')");
        page.Advance(3_000);
        page.Run("dispatch(boxes()[1], 'mouseenter')");
        page.Advance(7_000);

        Assert.Equal(new[] { TimedOut(3, 1), TimedOut(1, 1) }, GoneSent(page));
        Assert.Equal(new[] { "2" }, page.Labels());
    }

    // ===== Staying out of the way =====

    /// <summary>The tool column's tips open to its right and the speed buttons' tips open upward, both into the deck.</summary>
    [Theory]
    [InlineData(".vertical-tools")]
    [InlineData(".speed-controls")]
    public void While_the_pointer_is_on_buttons_whose_tips_open_into_the_deck_it_steps_aside_so_the_tips_can_be_read(string buttons)
    {
        var page = new PageScript();
        page.Run("var hud = addHud(1)");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));

        page.Run($"pointAt(hud, '{buttons}')");
        var onTools = LastRects(page);
        var isHiddenOnTools = page.Text("stack().classList.contains('etk-hidden')");
        page.Run("pointAt(hud, '.queue-container')");

        Assert.Empty(onTools.Boxes);
        Assert.Equal("true", isHiddenOnTools);
        Assert.Single(LastRects(page).Boxes);
        Assert.Equal("false", page.Text("stack().classList.contains('etk-hidden')"));
    }

    /// <summary>
    /// The HUD is watched before any toast, so a pointer already on the tools when one comes counts. A pointer that
    /// leaves without the page hearing it (the window loses it) must not keep the deck away for good, so the deck
    /// comes back once the pointer has been still for a few seconds; moving on the buttons keeps it aside.
    /// </summary>
    [Fact]
    public void A_pointer_already_on_the_tools_when_a_toast_comes_keeps_the_deck_aside_only_while_it_keeps_moving_there()
    {
        var page = new PageScript();
        page.Run("var hud = addHud(1)");
        page.Advance(1_000);

        page.Run("pointAt(hud, '.vertical-tools')");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina, milliseconds: 60_000)));
        var atFirst = LastRects(page);
        page.Advance(3_000);
        page.Run("pointAt(hud, '.vertical-tools', 'mousemove')");
        page.Advance(3_000);
        var whileMoving = LastRects(page);
        page.Advance(1_000);

        Assert.Empty(atFirst.Boxes);
        Assert.Empty(whileMoving.Boxes);
        Assert.Single(LastRects(page).Boxes);
    }

    /// <summary>
    /// Over an open panel's buttons the deck stays in sight, since its news can matter, but the panel takes every click
    /// under it. Only a page drawn above the HUD counts as a panel.
    /// </summary>
    [Theory]
    [InlineData(4, 0.05, true)]
    [InlineData(4, 0.60, false)]
    [InlineData(0, 0.05, false)]
    public void Over_a_panel_with_buttons_under_it_the_deck_shows_and_lets_clicks_through_until_the_buttons_go(int panelZ, double buttonX, bool isThrough)
    {
        var page = new PageScript();
        page.Run("addHud(1)");
        page.Run($"addFrame('BattleBag', 'https://webui/UI/BattleBag/BattleBag.html', {panelZ})");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));

        page.Run($"window.syncInteractiveRects('BattleBag', [{{ x: {buttonX.ToString(CultureInfo.InvariantCulture)}, y: 0.86, w: 0.1, h: 0.02 }}], null)");
        page.NextFrame();
        var whileOpen = LastRects(page);
        var isShownWhileOpen = page.Text("stack().classList.contains('etk-hidden')") == "false";
        var isThroughWhileOpen = page.Text("stack().classList.contains('etk-through')") == "true";
        page.Run("window.syncInteractiveRects('BattleBag', [], null)");
        page.NextFrame();

        Assert.Equal(isThrough ? 0 : 1, whileOpen.Boxes.Count);
        Assert.True(isShownWhileOpen);
        Assert.Equal(isThrough, isThroughWhileOpen);
        Assert.Single(LastRects(page).Boxes);
        Assert.Equal("false", page.Text("stack().classList.contains('etk-through')"));
        Assert.Equal("BattleBag,BattleBag", page.Text("syncedPages.join(',')"));
    }

    /// <summary>
    /// Most of the game's windows, a bench among them, never report their buttons: the game hands them every click on
    /// the screen. The deck is drawn over such a window and takes none of its clicks. Only a page shown above the HUD
    /// and taking the pointer counts.
    /// </summary>
    [Theory]
    [InlineData(4, "auto", true)]
    [InlineData(0, "auto", false)]
    [InlineData(4, "none", false)]
    public void Over_a_window_above_the_hud_that_reports_no_buttons_the_deck_shows_and_takes_no_clicks_until_it_closes(int windowZ, string pointerEvents, bool isThrough)
    {
        var page = new PageScript();
        page.Run("addHud(1)");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));

        page.Run($"var shop = addFrame('Shop', 'https://webui/UI/Shop/Shop.html', {windowZ}); shop.style.pointerEvents = '{pointerEvents}'");
        page.Advance(1_000);
        var whileOpen = LastRects(page);
        var isShownWhileOpen = page.Text("stack().classList.contains('etk-hidden')") == "false";
        var isThroughWhileOpen = page.Text("stack().classList.contains('etk-through')") == "true";
        page.Run("shop.style.display = 'none'");
        page.Advance(250);

        Assert.Equal(isThrough ? 0 : 1, whileOpen.Boxes.Count);
        Assert.True(isShownWhileOpen);
        Assert.Equal(isThrough, isThroughWhileOpen);
        Assert.Single(LastRects(page).Boxes);
    }

    /// <summary>A window that reported its buttons before the toolkit was there is asked again, once, by a resize.</summary>
    [Fact]
    public void A_window_not_yet_heard_from_is_asked_for_its_buttons_and_the_deck_comes_back_when_none_are_under_it()
    {
        var page = new PageScript();
        page.Run("addHud(1); reportOnResize(addFrame('BattleBag', 'https://webui/UI/BattleBag/BattleBag.html', 4), [{ x: 0.60, y: 0.86, w: 0.1, h: 0.02 }])");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));

        var beforeAsking = LastRects(page);
        page.Advance(2_000);

        Assert.Empty(beforeAsking.Boxes);
        Assert.Single(LastRects(page).Boxes);
        Assert.Equal("BattleBag", page.Text("syncedPages.join(',')"));
    }

    /// <summary>The game opens a window again under the same page id; what it reported last time says nothing now.</summary>
    [Fact]
    public void A_window_opened_again_is_judged_by_what_it_reports_now_not_by_what_it_reported_before_it_closed()
    {
        var page = new PageScript();
        page.Run("addHud(1); var bag = addFrame('BattleBag', 'https://webui/UI/BattleBag/BattleBag.html', 4)");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));
        page.Run("window.syncInteractiveRects('BattleBag', [{ x: 0.60, y: 0.86, w: 0.1, h: 0.02 }], null)");
        page.NextFrame();

        var firstTime = LastRects(page);
        page.Run("frames.splice(frames.indexOf(bag), 1)");
        page.Advance(1_000);
        page.Run("reportOnResize(addFrame('BattleBag', 'https://webui/UI/BattleBag/BattleBag.html', 4), [{ x: 0.05, y: 0.86, w: 0.1, h: 0.02 }])");
        page.Advance(1_000);

        Assert.Single(firstTime.Boxes);
        Assert.Empty(LastRects(page).Boxes);
        Assert.Equal("BattleBag,BattleBag", page.Text("syncedPages.join(',')"));
    }

    private static PageCommand.Rects LastRects(PageScript page) => page.Sent.OfType<PageCommand.Rects>().Last();

    /// <summary>A reported box back in page pixels, rounded.</summary>
    private static (int Left, int Top, int Width, int Height, int Bottom) Px(NormalRect box) =>
        ((int)Math.Round(box.X * 1920), (int)Math.Round(box.Y * 1080), (int)Math.Round(box.W * 1920), (int)Math.Round(box.H * 1080), (int)Math.Round((box.Y + box.H) * 1080));

    // ===== Keeping the game smooth =====

    /// <summary>Counts each time the page is made to work out where the deck is on screen.</summary>
    private const string CountDeckMeasures =
        "var measured = 0; (function () { var s = stack(); var read = s.getBoundingClientRect; s.getBoundingClientRect = function () { measured++; return read.call(s); }; })()";

    [Fact]
    public void With_no_toasts_up_pages_reporting_their_buttons_reach_the_game_without_the_deck_being_measured()
    {
        var page = new PageScript();
        page.Run("addHud(1); addFrame('BattleBag', 'https://webui/UI/BattleBag/BattleBag.html', 4)");
        page.Run(CountDeckMeasures);

        page.Run("for (var i = 0; i < 3; i++) window.syncInteractiveRects('BattleBag', [{ x: 0.05, y: 0.86, w: 0.1, h: 0.02 }], null)");
        page.NextFrame();
        var measured = page.Number("measured");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));

        Assert.Equal(0, measured);
        Assert.Equal("BattleBag,BattleBag,BattleBag", page.Text("syncedPages.join(',')"));
        // What the panel reported is still known: the first toast lets clicks through to its buttons.
        Assert.Empty(LastRects(page).Boxes);
    }

    [Fact]
    public void Many_reports_in_one_frame_measure_the_deck_no_more_than_one_report_does()
    {
        var page = new PageScript();
        page.Run("addHud(1); addFrame('BattleBag', 'https://webui/UI/BattleBag/BattleBag.html', 4)");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, Stamina)));
        page.Advance(400);
        page.Run(CountDeckMeasures);

        page.Run("window.syncInteractiveRects('BattleBag', [{ x: 0.60, y: 0.86, w: 0.1, h: 0.02 }], null)");
        page.NextFrame();
        var afterOne = page.Number("measured");
        page.Run("measured = 0; for (var i = 0; i < 3; i++) window.syncInteractiveRects('BattleBag', [{ x: 0.60, y: 0.86, w: 0.1, h: 0.02 }], null)");
        page.NextFrame();

        Assert.True(afterOne > 0);
        Assert.Equal(afterOne, page.Number("measured"));
    }

    [Fact]
    public void While_toasts_are_up_the_deck_is_not_laid_out_again_when_nothing_about_it_changed()
    {
        var page = new PageScript();
        page.Run("addHud(1)");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, "1")), new PageOp.Show(View(2, "2")));
        page.Advance(400);

        var before = page.Number("boxes()[1].added['etk-behind']");
        page.Advance(1_000);

        Assert.Equal(before, page.Number("boxes()[1].added['etk-behind']"));
    }

    [Fact]
    public void A_card_that_grows_on_its_own_moves_the_card_behind_it_within_a_quarter_second()
    {
        var page = new PageScript();
        page.Run("addHud(1)");
        page.Apply(new PageOp.Place(ToastPosition.Hud), new PageOp.Show(View(1, "1")), new PageOp.Show(View(2, "2")));
        page.Advance(400);

        var before = Px(LastRects(page).Boxes[1]);
        page.Run("boxes()[0].extraHeight = 20");
        page.Advance(250);
        var after = Px(LastRects(page).Boxes[1]);

        Assert.Equal(964, before.Bottom);
        Assert.Equal(944, after.Bottom);
    }

    // ===== Item chips and the time bar =====

    private static ToastView WithItems(long id, params ToastItem[] items) =>
        View(id, "Wish granted") with { Content = new ToastContent("Notifications", "Wish granted", null, ToastKind.Success, null) { Items = ToastItems.From(items) } };

    [Fact]
    public void Item_chips_show_each_label_and_amount_and_a_toast_without_items_has_no_chip_row()
    {
        var page = new PageScript();

        page.Apply(
            new PageOp.Show(WithItems(1, new ToastItem("Iron Ore") { Count = 6, Icon = "Res/Item/ore.png" }, new ToastItem("<b>Cloth</b>"))),
            new PageOp.Show(View(2, Stamina)));

        Assert.Equal("Iron Ore x6|<b>Cloth</b>", page.Text("part(boxes()[1], 'etk-items').children.map(function (c) { return c.textContent; }).join('|')"));
        Assert.Equal("Res/Item/ore.png", page.Text("part(boxes()[1], 'etk-items').children[0].children[0].src"));
        Assert.Equal("null", page.Text("part(boxes()[0], 'etk-items')"));
    }

    [Fact]
    public void A_card_behind_the_front_one_lists_its_items_on_one_line()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(WithItems(1, new ToastItem("Iron Ore") { Count = 6 }, new ToastItem("Cloth"))));

        Assert.Equal("Iron Ore x6, Cloth", page.Text("part(boxes()[0], 'etk-itemline').textContent"));
    }

    [Fact]
    public void Items_without_a_label_are_dropped_and_no_more_than_the_max_are_sent()
    {
        var items = Enumerable.Range(1, ToastItem.Max + 3).Select(n => new ToastItem("Item " + n)).Prepend(new ToastItem("")).ToArray();

        var kept = ToastItems.From(items).List;
        var script = ToastScript.Apply(new PageOp[] { new PageOp.Show(View(1, Stamina)) });

        Assert.Equal(ToastItem.Max, kept.Count);
        Assert.Equal("Item 1", kept[0].Label);
        Assert.DoesNotContain("\"items\"", script);
    }

    [Fact]
    public void The_time_bar_stands_still_while_the_toast_is_hovered()
    {
        var page = new PageScript();

        page.Apply(new PageOp.Show(View(1, Stamina, milliseconds: 8_000)));
        page.Advance(2_000);
        page.Run("dispatch(boxes()[0], 'mouseenter')");

        Assert.Equal("75%", page.Text("part(boxes()[0], 'etk-bar').children[0].style.width"));
        Assert.Equal("none", page.Text("part(boxes()[0], 'etk-bar').children[0].style.transition"));
    }
}
