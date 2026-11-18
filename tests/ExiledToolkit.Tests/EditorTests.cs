using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// editor.js as the Toolkit embeds it, run in Jint: the reward editor two tabs share. Each tab that makes one gets its
/// own effect list, its own class name in the styles with its own lines where they belong, and an older copy of the
/// script never replaces a newer one a mod already sent.
/// </summary>
public class EditorTests
{
    private readonly Engine _engine = new();

    public EditorTests()
    {
        _engine.Execute("var window = this;");
    }

    [Fact]
    public void Each_tab_gets_its_own_effect_list_with_the_games_effects_in_it()
    {
        Load();

        _engine.Execute("var a = window.ExiledEditor.make({}), b = window.ExiledEditor.make({});");
        _engine.Execute("a.fx({ id: 'mr:only-here', src: 'mr', cat: 'home', scripts: [], params: [] });");

        Assert.True(Bool("a.FX.some(f => f.id === 'game:solar') && b.FX.some(f => f.id === 'game:solar')"));
        Assert.True(Bool("a.FX.some(f => f.id === 'mr:only-here')"));
        Assert.False(Bool("b.FX.some(f => f.id === 'mr:only-here')"));
    }

    [Fact]
    public void The_styles_carry_the_tabs_class_and_its_own_lines_at_their_place()
    {
        Load();

        _engine.Execute("var css = window.ExiledEditor.make({}).editorCss('mr', ['N .mine{color:red}']);");

        Assert.True(Bool("css.indexOf('N .mine{color:red}') > 0 && css.indexOf('N .mine{color:red}') < css.length - 1"));
        Assert.True(Bool("css.some(l => l.indexOf('.mr-top') >= 0) && css.some(l => l.indexOf('.tg.src-mr{') >= 0)"));
        Assert.False(Bool("css.some(l => l.indexOf('{c}') >= 0 || l === '@extra')"));
    }

    [Fact]
    public void An_older_copy_never_replaces_a_newer_one()
    {
        _engine.Execute("window.ExiledEditor = { version: 2, make: function () { return 'newer'; } };");

        Load();

        Assert.Equal("newer", _engine.Evaluate("window.ExiledEditor.make()").AsString());
    }

    [Fact]
    public void The_search_finds_a_reward_by_its_number_or_any_levels_text_in_either_language()
    {
        Load();
        MakeTabWithTwoRewards();

        Assert.Equal("102", Found("loot"));
        Assert.Equal("101", Found("太阳能"));
        Assert.Equal("102", Found("102"));
        Assert.Equal("101", Found("SOLAR"));
    }

    [Fact]
    public void The_search_needs_every_word_and_an_empty_search_shows_every_reward()
    {
        Load();
        MakeTabWithTwoRewards();

        Assert.Equal("", Found("solar loot"));
        Assert.Equal("101,102", Found("  "));
    }

    [Fact]
    public void A_view_with_no_rewards_leaves_the_editor_empty()
    {
        Load();
        MakeTabWithTwoRewards();
        _engine.Execute("T.st.sel = 102; T.st.view = 'file'; tab.keepSelectionInView();");

        Assert.Equal(0, Number("T.st.sel"));
    }

    [Fact]
    public void A_view_without_the_open_reward_opens_its_first_row_at_level_one()
    {
        Load();
        MakeTabWithTwoRewards();
        _engine.Execute("""
            T.st.entries = [{ id: 105, levels: [{}], gives: [] }, { id: 103, levels: [{}], gives: [] }];
            T.st.sel = 102; T.st.lv = 1; T.st.view = 'file'; tab.keepSelectionInView();
            """);

        Assert.Equal(103, Number("T.st.sel"));
        Assert.Equal(0, Number("T.st.lv"));
    }

    [Fact]
    public void A_search_that_hides_the_open_reward_keeps_it_open()
    {
        Load();
        MakeTabWithTwoRewards();
        _engine.Execute("T.st.sel = 101; T.st.lv = 0; T.st.q = 'loot'; tab.keepSelectionInView();");

        Assert.Equal(101, Number("T.st.sel"));
    }

    [Fact]
    public void Removing_a_change_to_a_game_reward_closes_it_in_the_changed_view()
    {
        Load();
        MakeTabWithTwoRewards();
        _engine.Execute("T.st.sel = 102; T.st.lv = 0; T.st.view = 'file'; tab.afterRemove();");

        Assert.Equal(0, Number("T.st.sel"));
    }

    [Fact]
    public void A_game_reward_with_levels_shows_what_each_level_costs_and_does()
    {
        Load();
        MakeTabWithTwoRewards();
        _engine.Execute("""
            var wind = { id: 107, levels: [
              { cost: 10, gameDesc: { en: 'Wind turbines make 15% more power.' } },
              { cost: 16, gameDesc: { en: 'Wind turbines make 30% more power.' } },
              { cost: 24, gameDesc: { en: 'Wind turbines make 50% more power.' } }], gives: [] };
            T.st.lang = 'en'; T.st.lv = 0;
            var html = tab.ghostHtml(wind);
            """);

        Assert.True(Bool("html.indexOf('30% more power.') > 0 && html.indexOf('50% more power.') > 0"));
        Assert.True(Bool("html.indexOf('>24<') > 0 && html.indexOf('>50<') > 0"));
        Assert.True(Bool("html.indexOf('data-a=\"lv\" data-v=\"2\"') > 0"));
        Assert.False(Bool("tab.ghostHtml(ghosts[101]).indexOf('data-a=\"lv\"') >= 0"));
    }

    // Sunlight Collection I, II and III each add 15% more solar power, and the cards all say 15%. The player with all
    // three gets 45%, so the levels table has to say so when the tab's levels add up.
    private void MakeSunlightReward(string per) => _engine.Execute($$"""
        var sunlight = { id: 31271, levels: [
          { cost: 10, gameDesc: { en: 'Solar panel output increased by 15%.' } },
          { cost: 16, gameDesc: { en: 'Solar panel output increased by 15%.' } },
          { cost: 24, gameDesc: { en: 'Solar panel output increased by 15%.' } }],
          gives: [{ fx: tab.FX.find(f => f.id === 'game:solar'), per: { FloatValue0: {{per}} }, pick: {} }] };
        T.st.lang = 'en'; T.st.lv = 0;
        """);

    [Fact]
    public void Levels_that_add_up_show_the_total_at_each_level()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        MakeSunlightReward("[15, 15, 15]");

        _engine.Execute("var html = tab.ghostHtml(sunlight);");

        Assert.True(Bool("html.indexOf('IN TOTAL') > 0"));
        Assert.True(Bool("html.indexOf('>+15%<') > 0 && html.indexOf('>+30%<') > 0 && html.indexOf('>+45%<') > 0"));
        Assert.Equal("+45%", _engine.Evaluate("tab.totalText(sunlight, 2, false)").AsString());
    }

    [Fact]
    public void A_level_past_the_last_value_keeps_adding_the_last_value()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        MakeSunlightReward("[10, 5]");

        Assert.Equal("+20%", _engine.Evaluate("tab.totalText(sunlight, 2, false)").AsString());
    }

    [Fact]
    public void Levels_that_replace_each_other_show_no_total()
    {
        Load();
        MakeTabWithTwoRewards();
        MakeSunlightReward("[15, 30, 45]");

        _engine.Execute("var html = tab.ghostHtml(sunlight);");

        Assert.False(Bool("html.indexOf('IN TOTAL') >= 0"));
        Assert.False(Bool("html.indexOf('+90%') >= 0"));
        Assert.True(Bool("html.indexOf('WHAT IT DOES') > 0"));
    }

    [Fact]
    public void A_locked_give_shows_no_total()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        MakeSunlightReward("[15, 15, 15]");

        _engine.Execute("sunlight.givesLocked = true;");

        Assert.False(Bool("tab.addsUp(sunlight)"));
    }

    // The game multiplies the price by what each level leaves to pay (0.9 for 10% off), so two 10% levels make 19%.
    [Fact]
    public void Discount_levels_multiply()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        MakeSunlightReward("[15, 15, 15]");
        _engine.Execute("""
            var discount = { id: 500, levels: sunlight.levels, gives: [{ fx: tab.FX.find(f => f.id === 'game:short-life-discount'), per: { FloatValue1: [10, 10, 10], FloatValue0: [5, 5, 5] }, pick: {} }] };
            """);

        Assert.Equal("-19%", _engine.Evaluate("tab.totalText(discount, 1, false)").AsString());
        Assert.Equal("-27.1%", _engine.Evaluate("tab.totalText(discount, 2, false)").AsString());
        Assert.Contains("less than 5 days", _engine.Evaluate("tab.autoDesc(discount, 2, false)").AsString());
        Assert.Contains("Discounts multiply", _engine.Evaluate("tab.ghostHtml(discount)").AsString());
        Assert.DoesNotContain("Discounts multiply", _engine.Evaluate("tab.ghostHtml(sunlight)").AsString());
    }

    // Each level of Start with a skill level raises the skill to its own number, so level II at 2 gives 2, not 1 + 2.
    [Fact]
    public void Skill_start_levels_count_only_the_highest()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        _engine.Execute("""
            var skill = { id: 501, levels: [{ cost: 10, gameDesc: { en: 'Production starts higher.' } }, { cost: 16, gameDesc: { en: 'Production starts higher.' } }], gives: [{ fx: tab.FX.find(f => f.id === 'game:skill-start'), per: { IntValue0: [1, 2] }, pick: { StrValue0: 'Production' } }] };
            var levels = tab.FX.find(f => f.id === 'game:skill-start').params.find(p => p.key === 'IntValue0');
            """);

        Assert.Equal("+2 Lv", _engine.Evaluate("tab.totalText(skill, 1, false)").AsString());
        Assert.Equal(2, Number("tab.defaultAt(levels, 1)"));
        Assert.Contains("the highest level counts", _engine.Evaluate("tab.ghostHtml(skill)").AsString());
    }

    // A loan limit of 800 is 500 plus a raise of 300 in the file, and the game adds the raises of every level.
    [Fact]
    public void Loan_levels_each_raise_the_limit_and_add_up()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        _engine.Execute("""
            var loanFx = tab.FX.find(f => f.id === 'game:loan');
            var loan = { id: 502, levels: [{ cost: 10 }, { cost: 16 }], gives: [{ fx: loanFx, per: { IntValue0: [300, 300] }, pick: {} }] };
            """);

        Assert.Equal("+600", _engine.Evaluate("tab.totalText(loan, 1, false)").AsString());
        Assert.Equal("Raises the loan limit by 300 (from 500). In total: +600.", _engine.Evaluate("tab.autoDesc(loan, 1, false)").AsString());
        Assert.Equal(300, Number("tab.display(loanFx.params[0], 300)"));
    }

    [Fact]
    public void A_loan_reads_as_the_new_limit_where_levels_replace_each_other()
    {
        Load();
        MakeTabWithTwoRewards();

        _engine.Execute("var loanFx = tab.FX.find(f => f.id === 'game:loan');");

        Assert.Equal("Loan limit increased to 800 (from 500).", _engine.Evaluate("loanFx.desc({ IntValue0: 800 }, false)").AsString());
        Assert.Equal(800, Number("tab.display(loanFx.params[0], 300)"));
    }

    // A tab that turns levelsAddUp on after make still gets the right total from a number with a base, 500 + 300 + 300.
    [Fact]
    public void A_total_of_numbers_with_a_base_counts_the_base_once()
    {
        Load();
        MakeTabWithTwoRewards();
        _engine.Execute("T.levelsAddUp = true;");
        _engine.Execute("var loan = { id: 502, levels: [{ cost: 10 }, { cost: 16 }], gives: [{ fx: tab.FX.find(f => f.id === 'game:loan'), per: { IntValue0: [800, 800] }, pick: {} }] };");

        Assert.Equal("1100", _engine.Evaluate("tab.totalText(loan, 1, false)").AsString());
    }

    // The game spoils backpack food at 1 / (1 + the sum of every level), so each level of 1 is 100% longer.
    [Fact]
    public void Backpack_levels_each_add_to_the_shelf_life()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        _engine.Execute("var pack = { id: 503, levels: [{ cost: 10 }, { cost: 16 }], gives: [{ fx: tab.FX.find(f => f.id === 'game:backpack-life'), per: { FloatValue0: [100, 100] }, pick: {} }] };");

        Assert.Equal("+200%", _engine.Evaluate("tab.totalText(pack, 1, false)").AsString());
        Assert.Equal("Items in your backpack keep 100% longer. In total: +200%.", _engine.Evaluate("tab.autoDesc(pack, 1, false)").AsString());
    }

    // Solar starts at 20, 30, 40 where levels replace each other. Where they add up, that would make level III 90%.
    [Fact]
    public void A_new_effect_starts_at_the_same_number_on_every_level_where_levels_add_up()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        _engine.Execute("var solar = tab.FX.find(f => f.id === 'game:solar'), more = solar.params[0];");

        Assert.Equal(20, Number("tab.defaultAt(more, 2)"));
        Assert.Equal("20 % each level", _engine.Evaluate("tab.paramRow(more, 3).replace(/<[^>]+>/g, '|').split('|').filter(Boolean)[1]").AsString());

        _engine.Execute("var g = { fx: solar, per: { FloatValue0: [20, 20, 20] }, pick: {} };");

        Assert.Equal("Each level: +20%. In total: +20% / +40% / +60%", _engine.Evaluate("tab.readsLine(g, 3, false)").AsString());
    }

    [Fact]
    public void A_new_effect_keeps_climbing_defaults_where_levels_replace_each_other()
    {
        Load();
        MakeTabWithTwoRewards();
        _engine.Execute("var solar = tab.FX.find(f => f.id === 'game:solar'), more = solar.params[0];");

        Assert.Equal(40, Number("tab.defaultAt(more, 2)"));

        _engine.Execute("var g = { fx: solar, per: { FloatValue0: [20, 30, 40] }, pick: {} };");

        Assert.Equal("+20% / +30% / +40%", _engine.Evaluate("tab.readsLine(g, 3, false)").AsString());
    }

    // A level's card says what that level gives, then what the player has in total with the levels before it.
    [Fact]
    public void A_level_card_names_its_total_from_the_second_level_on()
    {
        Load();
        MakeTabWithTwoRewards(levelsAddUp: true);
        MakeSunlightReward("[15, 15, 15]");

        Assert.DoesNotContain("In total", _engine.Evaluate("tab.autoDesc(sunlight, 0, false)").AsString());
        Assert.EndsWith(" In total: +30%.", _engine.Evaluate("tab.autoDesc(sunlight, 1, false)").AsString());
        Assert.EndsWith("（累计+45%）", _engine.Evaluate("tab.autoDesc(sunlight, 2, true)").AsString());
        Assert.DoesNotContain("In total", _engine.Evaluate("tab.plainDesc(sunlight, 1, false)").AsString());
    }

    [Fact]
    public void A_level_card_names_no_total_where_levels_replace_each_other()
    {
        Load();
        MakeTabWithTwoRewards();
        MakeSunlightReward("[15, 30, 45]");

        Assert.DoesNotContain("In total", _engine.Evaluate("tab.autoDesc(sunlight, 2, false)").AsString());
    }

    [Fact]
    public void Enter_and_Down_in_the_search_box_follow_the_order_the_tab_draws()
    {
        Load();
        MakeTabWithTwoRewards();

        // A tab that draws 102 above 101, the way New Loop Plus puts its first column above its second.
        _engine.Execute("T.compareIds = (a, b) => b - a; T.render = () => {};");
        _engine.Execute("var key = k => tab.onSearchKey({ key: k, target: { getAttribute: () => 'rq' }, preventDefault: () => {} });");

        Assert.Equal("102,101", Found(""));

        _engine.Execute("key('Enter');");
        Assert.Equal(102, _engine.Evaluate("T.st.sel").AsNumber());

        _engine.Execute("key('ArrowDown');");
        Assert.Equal(101, _engine.Evaluate("T.st.sel").AsNumber());
    }

    [Fact]
    public void The_clear_button_empties_the_search_box()
    {
        Load();
        MakeTabWithTwoRewards();
        GiveTabASearchBox("solar");

        _engine.Execute("tab.clearSearch();");

        Assert.Equal("", _engine.Evaluate("T.st.q").AsString());
        Assert.Equal("", _engine.Evaluate("box.value").AsString());
    }

    [Fact]
    public void Esc_in_the_search_box_clears_it_and_keeps_the_key_from_the_Mod_Manager()
    {
        Load();
        MakeTabWithTwoRewards();
        GiveTabASearchBox("solar");

        // The Mod Manager's Esc takes the cursor out of any box it reaches, so the search has to stop the key first.
        _engine.Execute("var stopped = false; tab.onSearchKey({ key: 'Escape', target: box, preventDefault: () => {}, stopPropagation: () => { stopped = true; } });");

        Assert.True(Bool("stopped"));
        Assert.Equal("", _engine.Evaluate("T.st.q").AsString());
        Assert.Equal("", _engine.Evaluate("box.value").AsString());
    }

    [Fact]
    public void Esc_in_an_empty_search_box_goes_on_to_the_Mod_Manager()
    {
        Load();
        MakeTabWithTwoRewards();
        GiveTabASearchBox("");

        _engine.Execute("var stopped = false; tab.onSearchKey({ key: 'Escape', target: box, preventDefault: () => {}, stopPropagation: () => { stopped = true; } });");

        Assert.False(Bool("stopped"));
    }

    [Fact]
    public void A_change_made_by_pressing_a_reward_redraws_only_after_the_click_has_landed()
    {
        Load();
        MakeTabWithTwoRewards();
        GiveTabAPage();

        // Leaving a number field for a reward in the list: mousedown, the field's change, mouseup, then the click.
        // Timers run while the button is still held, as they would during a slow click.
        _engine.Execute("rootOn.mousedown(); tab.renderAfterChange(); runTimers();");
        Assert.Equal(0, Number("renders"));

        _engine.Execute("docOn.mouseup();");
        Assert.Equal(0, Number("renders"));

        _engine.Execute("runTimers();");
        Assert.Equal(1, Number("renders"));

        // Once the press is over, a change made from the keyboard does not wait for another one.
        _engine.Execute("tab.renderAfterChange(); runTimers();");
        Assert.Equal(2, Number("renders"));
    }

    [Fact]
    public void A_change_made_by_Tab_redraws_and_puts_the_cursor_back_in_the_next_field()
    {
        Load();
        MakeTabWithTwoRewards();
        GiveTabAPage();

        // Tab has already moved the cursor to the cost field of level 2 when the redraw runs.
        _engine.Execute("doc.activeElement = field('val', 'cost:1', 3);");
        _engine.Execute("tab.renderAfterChange(); runTimers();");

        Assert.Equal(1, Number("renders"));
        Assert.Equal("input[data-a=\"val\"][data-k=\"cost:1\"]", _engine.Evaluate("focusedQuery").AsString());
        Assert.Equal(3, Number("caret"));
    }

    [Fact]
    public void Typing_a_number_lights_SAVE_before_the_number_is_written()
    {
        Load();
        MakeTabWithTwoRewards();
        GiveTabAPage();

        _engine.Execute("T.st.dirty = false; tab.onInput({ target: field('val', 'cost:0', 1) });");

        Assert.True(Bool("saveLit"));
    }

    [Fact]
    public void Effect_text_says_one_hour_or_one_day_without_a_plural()
    {
        Load();

        _engine.Execute("var t = window.ExiledEditor.make({}), byId = id => t.FX.find(f => f.id === id), countdown = byId('game:countdown'), shelf = byId('game:short-life-discount');");

        Assert.Contains("Gives you 1 hour of", _engine.Evaluate("countdown.desc({ IntValue0: 1 }, false)").AsString());
        Assert.Contains("Gives you 3 hours of", _engine.Evaluate("countdown.desc({ IntValue0: 3 }, false)").AsString());
        Assert.Contains("less than 1 day.", _engine.Evaluate("shelf.desc({ FloatValue0: 1, FloatValue1: 10 }, false)").AsString());
        Assert.Contains("less than 5 days.", _engine.Evaluate("shelf.desc({ FloatValue0: 5, FloatValue1: 10 }, false)").AsString());
    }

    // The plural of a few words puts the s on the last one: "2 more times", never "{0|more time}" left in the text.
    [Fact]
    public void A_plural_of_several_words_puts_the_s_on_the_last_word()
    {
        Load();

        _engine.Execute("var t = window.ExiledEditor.make({}), eats = t.FX.find(f => f.id === 'game:AE_TiredOfFoodExtraEats');");

        Assert.Contains("the same food 1 more time before", Str("eats.desc({ FloatValue0: 1 }, false)"));
        Assert.Contains("the same food 2 more times before", Str("eats.desc({ FloatValue0: 2 }, false)"));
    }

    // Every effect in the list reads as finished text at its first default, in both languages: nothing like {0},
    // undefined or NaN left for the player to see.
    [Fact]
    public void Every_effect_reads_its_default_numbers_as_plain_text_in_both_languages()
    {
        Load();

        _engine.Execute("""
            var t = window.ExiledEditor.make({ st: { lang: 'en', game: [], byId: {}, scripts: new Set() } }), bad = [];
            t.FX.forEach(f => {
              var v = {};
              f.params.forEach(p => { v[p.key] = Array.isArray(p.def) ? p.def[0] : p.def; });
              [false, true].forEach(zh => {
                var text = f.line(v, zh) + ' ' + f.desc(v, zh, f.params);
                if (/\{\d|undefined|NaN/.test(text)) bad.push(f.id + (zh ? ' zh: ' : ' en: ') + text);
              });
            });
            """);

        Assert.Equal("", Str("bad.join(' | ')"));
        Assert.True(Number("t.FX.length") > 200);
    }

    // A tab that adds an effect with an id already in the list replaces it in place, so a mod can reword one.
    [Fact]
    public void An_effect_with_an_id_already_in_the_list_replaces_it()
    {
        Load();

        _engine.Execute("""
            var t = window.ExiledEditor.make({}), before = t.FX.length, at = t.FX.findIndex(f => f.id === 'game:solar');
            t.fx({ id: 'game:solar', cat: 'home', script: 'AE_SolarOutput', en: 'Mine', zh: '我的', what: ['', ''], params: [], line: '', desc: '' });
            """);

        Assert.Equal(Number("before"), Number("t.FX.length"));
        Assert.Equal("Mine", Str("t.FX[at].en"));
    }

    // One game script can be several effects, told apart by a fixed field (the status it changes, the skill a recipe
    // is for). A row with a value none of them takes is shown as the unnamed script instead of a wrong name.
    [Fact]
    public void A_game_row_finds_the_effect_its_fixed_fields_belong_to()
    {
        Load();

        _engine.Execute("var t = window.ExiledEditor.make({ isAvailable: () => true, st: { lang: 'en', game: [], byId: {} } }), id = (s, v) => t.match(s, v).id;");

        Assert.Equal("game:Passive_Hunger", Str("id('Passive_Hunger', { IntValue0: 1001, IntValue1: 30, IntValue2: 30 })"));
        Assert.Equal("raw:Passive_Dismay", Str("id('Passive_Dismay', { IntValue0: 1016, IntValue1: 30, IntValue2: 30 })"));
        Assert.Equal("game:Buff_Gratitude-wish", Str("id('Buff_Gratitude', { IntValue0: 1009, IntValue1: 1017, FloatValue0: 7200 })"));
        Assert.Equal("game:Buff_Gratitude-appetite", Str("id('Buff_Gratitude', { IntValue0: 200, FloatValue0: -3600 })"));
        Assert.Equal("game:cook-recipe", Str("id('UnlockCraftRecipe', { StrValue0: 'Cooking', StrValue1: '2016' })"));
        Assert.Equal("game:UnlockCraftRecipe", Str("id('UnlockCraftRecipe', { StrValue0: 'Production', StrValue1: '305;337' })"));
    }

    // The three flowerpot sizes are one effect with a pick, and each size's script reads back as that size.
    [Fact]
    public void Each_flowerpot_script_reads_back_as_its_own_size()
    {
        Load();

        _engine.Execute("""
            var t = window.ExiledEditor.make({ isAvailable: () => true, st: { lang: 'en', game: [], byId: {} } });
            var pot = t.FX.find(f => f.id === 'game:pot-no-till'), size = s => t.picksOf(pot)[0].from({}, s);
            """);

        Assert.Equal("game:pot-no-till", Str("t.match('AE_SkipTillSoilLargePot', {}).id"));
        Assert.Equal("game:pot-no-till", Str("t.match('AE_SkipTillSoilMediumPot', {}).id"));
        Assert.Equal("Medium", Str("size('AE_SkipTillSoilMediumPot')"));
        Assert.Equal("Large", Str("size('AE_SkipTillSoilLargePot')"));
        Assert.Equal("AE_SkipTillSoilLargePot", Str("pot.scriptOf({ pick: { pot: 'Large' } })"));
    }

    // Early Bedtime only gives more Stamina from rest, so its text leaves out the faster hunger the 3 day status has.
    [Fact]
    public void Rest_text_names_faster_hunger_only_when_the_reward_has_it()
    {
        Load();

        _engine.Execute("var t = window.ExiledEditor.make({}), rest = t.FX.find(f => f.id === 'game:Buff_306510');");

        Assert.Equal("For 3 days, resting gives 50% more Stamina.", Str("rest.desc({ FloatValue1: 50, FloatValue2: 0 }, false)"));
        Assert.Equal("For 3 days, resting gives 100% more Stamina, but Satiety drops 30% faster.", Str("rest.desc({ FloatValue1: 100, FloatValue2: 30 }, false)"));
    }

    // A script nothing names yet: its boxes are numbered from 1, a fraction its cards write as a percent shows as one,
    // and the first card's text says what it does.
    [Fact]
    public void An_unnamed_script_numbers_its_boxes_and_shows_the_percents_its_cards_use()
    {
        Load();

        _engine.Execute("""
            var game = [{ id: 9001, name: { en: 'Test Reward', zh: '测试奖励' }, levelText: { en: '30% / 60%', zh: '' }, levels: [
              { script: 'MadeUpScript', desc: { en: 'Use 30% less Stamina for 5 days.', zh: '5天内精力消耗降低30%' }, values: { FloatValue0: -0.3, FloatValue1: 0.5, IntValue0: 5 } },
              { script: 'MadeUpScript', desc: { en: 'Use 60% less Stamina for 5 days.', zh: '' }, values: { FloatValue0: -0.6, FloatValue1: 0.5, IntValue0: 5 } }] }];
            var t = window.ExiledEditor.make({ isAvailable: () => true, st: { lang: 'en', game: game, byId: { 9001: game[0] } } });
            var raw = t.match('MadeUpScript', game[0].levels[0].values), v = { FloatValue0: -30, FloatValue1: 0.5, IntValue0: 5 };
            """);

        Assert.Equal("raw:MadeUpScript", Str("raw.id"));
        Assert.Equal("Number 1: -30%, Number 2: 0.5, Number 3: 5", Str("raw.desc(v, false)"));
        Assert.Equal("数值 1：-30%，数值 2：0.5，数值 3：5", Str("raw.desc(v, true)"));
        Assert.Equal("-30,-60", Str("raw.params[0].def.join(',')"));
        Assert.Equal("Use 30% less Stamina for 5 days.", Str("raw.what[0]"));
    }

    // The game's card text for up to three rewards that run an effect: the first level that runs it, then the
    // values per level, or the last level's text when the reward has no such line.
    [Fact]
    public void The_game_card_text_shows_for_up_to_three_rewards_that_use_an_effect()
    {
        Load();

        _engine.Execute("""
            var lv = (script, en) => ({ script: script, desc: { en: en, zh: en + ' zh' }, values: {} });
            var game = [
              { id: 1, name: { en: 'Steady Hands', zh: '稳手' }, levelText: { en: '10% / 20%', zh: '' }, levels: [lv('S', 'Work 10% faster.'), lv('S', 'Work 20% faster.')] },
              { id: 2, name: { en: 'Quiet Steps', zh: '轻步' }, levels: [lv('Other', 'Something else.'), lv('S', 'Walk quietly.'), lv('S', 'Walk very quietly.')] },
              { id: 3, name: { en: 'Third', zh: '三' }, levels: [lv('S', 'Three.')] },
              { id: 4, name: { en: 'Fourth', zh: '四' }, levels: [lv('S', 'Four.')] }];
            var byId = {};
            game.forEach(g => { byId[g.id] = g; });
            var T = { st: { lang: 'en', game: game, byId: byId } }, t = window.ExiledEditor.make(T);
            var f = { scripts: ['S'], usedBy: [1, 2, 3, 4], finishes: [] }, html = t.gameCardsHtml(f);
            """);

        Assert.Contains("WHAT THE GAME SAYS", Str("html"));
        Assert.Contains(">Steady Hands<", Str("html"));
        Assert.Contains(">Work 10% faster.<", Str("html"));
        Assert.Contains("By level: 10% / 20%", Str("html"));
        Assert.Contains(">Walk quietly.<", Str("html"));
        Assert.Contains("Level 3: Walk very quietly.", Str("html"));
        Assert.DoesNotContain("Something else.", Str("html"));
        Assert.DoesNotContain("Four.", Str("html"));
        Assert.Equal("", Str("t.gameCardsHtml({ scripts: ['S'], usedBy: [], finishes: [] })"));

        _engine.Execute("T.st.lang = 'zh';");

        Assert.Contains("游戏内说明", Str("t.gameCardsHtml(f)"));
        Assert.Contains(">Work 10% faster. zh<", Str("t.gameCardsHtml(f)"));
        Assert.Contains("第3级：Walk very quietly. zh", Str("t.gameCardsHtml(f)"));
    }

    // An airdrop names its kind of items from the game's airdrop rewards. New Loop Plus finds the snacks airdrop at
    // 3066 and More Rewards at its first level, 30661. Either way the card says Snacks, never a group number.
    [Theory]
    [InlineData(3066)]
    [InlineData(30661)]
    public void An_airdrop_names_what_it_drops_in_either_tab(int snacksId)
    {
        Load();

        _engine.Execute("var snacksId = " + snacksId + ";" + """
            var byId = {};
            byId[snacksId] = { id: snacksId, name: { en: 'Airdrop - Snacks', zh: '空投补给·零食' }, levels: [] };
            var t = window.ExiledEditor.make({ st: { lang: 'en', game: [], byId: byId } });
            t.nameOptions();
            var drop = t.FX.find(f => f.id === 'game:airdrop'), v = { IntValue0: 201, IntValue1: 2 };
            """);

        Assert.Equal("A radio call brings 2 random Snacks the next day.", Str("drop.desc(v, false, drop.params)"));
        Assert.Contains("随机零食", Str("drop.desc(v, true, drop.params)"));
    }

    // Two of the game's rewards: 101 talks about solar power, and only 102's second level mentions loot. A tab whose
    // levels add up says so before it makes the editor, as More Rewards does.
    private void MakeTabWithTwoRewards(bool levelsAddUp = false) => _engine.Execute("var levelsAddUp = " + (levelsAddUp ? "true" : "false") + ";" + """
        var ghosts = {
          101: { id: 101, name: { en: 'Solar Boost', zh: '太阳能增益' }, levels: [{ gameDesc: { en: 'More solar power.', zh: '太阳能板产电效率提升' } }], gives: [] },
          102: { id: 102, name: { en: 'Lucky Find', zh: '幸运发现' }, levels: [{ gameDesc: { en: 'Find more food.' } }, { gameDesc: { en: 'Find more loot.' } }], gives: [] }
        };
        var T = { levelsAddUp: levelsAddUp, st: { q: '', view: 'all', entries: [] }, allIds: function () { return [102, 101]; }, ghostOf: function (id) { return ghosts[id] || null; } };
        var tab = window.ExiledEditor.make(T);
        """);

    // A tab root that records its listeners, the SAVE button and every focus, with timers that run only when asked.
    private void GiveTabAPage() => _engine.Execute("""
        var renders = 0, saveLit = false, focusedQuery = '', caret = -1, timers = [], rootOn = {}, docOn = {};
        var setTimeout = f => { timers.push(f); };
        var runTimers = () => { while (timers.length) timers.shift()(); };
        var field = (a, k, at) => ({
          tagName: 'INPUT', selectionStart: at, selectionEnd: at,
          hasAttribute: n => n === 'data-a' || n === 'data-k',
          getAttribute: n => n === 'data-a' ? a : n === 'data-k' ? k : null
        });
        var doc = { activeElement: null, addEventListener: (type, f) => { docOn[type] = f; } };
        var saveBtn = { classList: { toggle: (c, on) => { saveLit = on; } } };
        T.st.root = {
          ownerDocument: doc,
          contains: () => true,
          addEventListener: (type, f) => { rootOn[type] = f; },
          querySelector: q => q === '[data-a="save"]' ? saveBtn
            : { focus: () => { focusedQuery = q; }, setSelectionRange: (a, b) => { caret = a; } }
        };
        T.render = () => { renders++; };
        tab.watchPresses();
        """);

    private int Number(string expression) => (int)_engine.Evaluate(expression).AsNumber();

    // A tab root with only the search box in it, holding the given text.
    private void GiveTabASearchBox(string text) => _engine.Execute(
        "var box = { value: " + System.Text.Json.JsonSerializer.Serialize(text) + ", getAttribute: () => 'rq', focus: () => {}, setSelectionRange: () => {} };" +
        "T.st.q = box.value;" +
        "T.rowsHtml = () => '';" +
        "T.st.root = { querySelector: s => s === '[data-a=\"rq\"]' ? box : null };");

    private string Found(string search)
    {
        _engine.Execute("T.st.q = " + System.Text.Json.JsonSerializer.Serialize(search) + ";");

        return _engine.Evaluate("tab.listIds().join(',')").AsString();
    }

    private void Load() => _engine.Execute(Resource("ExiledToolkit.editor.js"));

    private bool Bool(string expression) => _engine.Evaluate(expression).AsBoolean();

    private string Str(string expression) => _engine.Evaluate(expression).AsString();

    private static string Resource(string name)
    {
        using var stream = typeof(EditorTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the tests");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
