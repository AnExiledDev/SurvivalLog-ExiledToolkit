using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ExiledToolkit.Core;
using Jint;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// The HUD button styles: dock.js drawing the tool column in the look the C# side sends (run in Jint on the fake root
/// page with observers and animation frames), and the settings' values, script and Mod Manager tags.
/// </summary>
public class HudStyleTests
{
    private const string PhonePath = "M17 1.01L7 1c-1.1 0-2 .9-2 2v18c0 1.1.9 2 2 2h10c1.1 0 2-.9 2-2V3c0-1.1-.9-1.99-2-1.99zM17 19H7V5h10v14z";
    private const string BagPath = "M20 6h-4V4c0-1.11-.89-2-2-2h-4c-1.11 0-2 .89-2 2v2H4c-1.11 0-1.99.89-1.99 2L2 19c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V8c0-1.11-.89-2-2-2zm-6 0h-4V4h4v2z";
    private const string CoreUi0BagPath = "M20 6h-3V4c0-1.11-.89-2-2-2h-4c-1.11 0-2 .89-2 2v2H4c-1.11 0-1.99.89-1.99 2L2 19c0 1.11.89 2 2 2h16c1.11 0 2-.89 2-2V8c0-1.11-.89-2-2-2zm-6 0h-4V4h4v2z";

    private readonly Engine _engine = new();

    public HudStyleTests()
    {
        _engine.Execute(Resource("ExiledToolkit.Tests.PhoneFake.js"));
        _engine.Execute(Resource("ExiledToolkit.Tests.HudStyleFake.js"));
        _engine.Execute($@"
            var hud = openFrame('file:///WebUI/UI/CoreUI1/CoreUI1.html');
            var page = makeDocument(hud.src);
            hud.contentDocument = page;
            var column = page.body.appendChild(page.createElement('div'));
            column.classList.add('vertical-tools');
            var h = addHudHelpers(page, column);
            var trap = h.hudButton(['interactive-btn', 'th-hud-btn']);
            var other = h.hudButton(['interactive-btn'], 'M5 5h14v14H5z');
            var phone = h.hudButton(['interactive-btn'], '{PhonePath}');
            phone.id = 'hudPhoneBtn';
            var bag = h.hudButton(['interactive-btn'], '{BagPath}');

            function kindOf(button) {{ return button.getAttribute('data-etk-ic') || '-'; }}
            function iconCount(button) {{ return button.children.filter(function (el) {{ return el.getAttribute('class') === 'etk-ic'; }}).length; }}
            function look(style, mono, extras) {{ window.__exiledtoolkitdock.look(style, mono, extras); }}");
        _engine.Execute(Resource("ExiledToolkit.dock.js"));
    }

    private void Run(string statements) => _engine.Execute(statements);

    private string Text(string expression) => _engine.Evaluate(expression).AsString();

    private bool Is(string expression) => _engine.Evaluate(expression).AsBoolean();

    private int Number(string expression) => (int)_engine.Evaluate(expression).AsNumber();

    private bool ColumnHas(string name) => Is($"column.classList.contains('{name}')");

    private string Css() => Text("page.getElementById('etk-dock-style').textContent");

    [Fact]
    public void Until_a_look_is_sent_the_column_is_left_as_the_game_draws_it()
    {
        Assert.False(ColumnHas("etk-st-lamplight"));
        Assert.Equal("-", Text("kindOf(phone)"));
        Assert.Equal(0, Number("iconCount(phone)"));
    }

    [Fact]
    public void Lamplight_marks_the_column_and_gives_the_phone_bag_and_mod_buttons_new_icons()
    {
        Run("look('Lamplight', false, true);");

        Assert.True(ColumnHas("etk-st-lamplight"));
        Assert.True(ColumnHas("etk-x"));
        Assert.False(ColumnHas("etk-mono"));
        Assert.Equal("trap", Text("kindOf(trap)"));
        Assert.Equal("phone", Text("kindOf(phone)"));
        Assert.Equal("bag", Text("kindOf(bag)"));
        Assert.Equal(1, Number("iconCount(trap)"));
        Assert.Equal(1, Number("iconCount(phone)"));
        Assert.Equal(1, Number("iconCount(bag)"));
        // The game's svg stays in the button for Vue; the stylesheet hides it.
        Assert.True(Is("phone.children[0].classList.contains('icon-svg')"));
    }

    [Fact]
    public void A_button_the_dock_does_not_know_keeps_its_own_icon_and_gets_no_mark()
    {
        Run("look('Signal', false, true);");

        Assert.Equal("-", Text("kindOf(other)"));
        Assert.Equal(0, Number("iconCount(other)"));
        Assert.Equal(1, Number("other.children.length"));
        Assert.Equal("M5 5h14v14H5z", Text("other.children[0].children[0].getAttribute('d')"));
    }

    [Theory]
    [InlineData("th-hud-btn", "trap")]
    [InlineData("pb-hud-btn", "package")]
    [InlineData("fg-hud-btn", "guide")]
    [InlineData("fh-hud-btn", "farm")]
    [InlineData("dm-hud-btn", "defense")]
    [InlineData("mso-hud-btn", "machines")]
    [InlineData("pm-hud-btn", "power")]
    [InlineData("sh-hud-btn", "storage")]
    public void Each_mod_button_gets_its_tools_icon(string buttonClass, string kind)
    {
        Run($"var mod = h.hudButton(['interactive-btn', '{buttonClass}']); look('Lamplight', false, true);");

        Assert.Equal(kind, Text("kindOf(mod)"));
        Assert.Equal(1, Number("iconCount(mod)"));
        Assert.Equal(Paths(kind), Number("h.iconOf(mod).children.length"));
    }

    [Fact]
    public void CoreUI0s_phone_and_bag_inside_their_tip_wrappers_get_icons_too()
    {
        Run($@"
            var wrapper = column.appendChild(page.createElement('div'));
            wrapper.classList.add('tooltip-wrapper');
            var oldPhone = h.hudButton(['phone-btn-c0'], 'M1 1h2v2H1z', wrapper);
            var oldBag = h.hudButton([], '{CoreUi0BagPath}', wrapper);
            look('FieldKit', false, false);");

        Assert.Equal("phone", Text("kindOf(oldPhone)"));
        Assert.Equal("bag", Text("kindOf(oldBag)"));
    }

    [Fact]
    public void A_new_look_is_drawn_at_once_without_doubling_the_icons()
    {
        Run("look('Lamplight', false, true); look('FieldKit', false, true);");

        Assert.True(ColumnHas("etk-st-fieldkit"));
        Assert.False(ColumnHas("etk-st-lamplight"));
        Assert.Equal(1, Number("iconCount(phone)"));
        Assert.Equal(1, Number("iconCount(trap)"));
    }

    [Fact]
    public void Classic_takes_off_the_classes_marks_and_icons()
    {
        Run("look('Lamplight', true, true); look('Classic', true, true);");

        Assert.False(ColumnHas("etk-st-lamplight"));
        Assert.False(ColumnHas("etk-mono"));
        Assert.False(ColumnHas("etk-x"));

        foreach (var button in new[] { "trap", "phone", "bag", "other" })
        {
            Assert.Equal("-", Text($"kindOf({button})"));
            Assert.Equal(0, Number($"iconCount({button})"));
            Assert.Equal(1, Number($"{button}.children.length"));
        }
    }

    [Fact]
    public void A_style_name_the_dock_does_not_know_is_drawn_as_Classic()
    {
        Run("look('Lamplight', false, true); look('Neon', false, true);");

        Assert.False(ColumnHas("etk-st-lamplight"));
        Assert.Equal(0, Number("iconCount(phone)"));
    }

    [Fact]
    public void An_icon_redrawn_by_the_game_gets_the_docks_icon_back_in_the_next_frame()
    {
        Run($"look('Lamplight', false, true); h.redraw(phone, '{PhonePath}'); flush();");

        Assert.Equal(0, Number("iconCount(phone)"));

        Run("nextFrame();");

        Assert.Equal(1, Number("iconCount(phone)"));
        Assert.Equal("phone", Text("kindOf(phone)"));
    }

    [Fact]
    public void An_icon_taken_out_on_its_own_comes_back_in_the_next_frame()
    {
        Run("look('Signal', false, true); bag.removeChild(h.iconOf(bag)); flush(); nextFrame();");

        Assert.Equal(1, Number("iconCount(bag)"));
    }

    [Fact]
    public void A_mod_button_added_later_is_dressed_in_the_next_frame()
    {
        Run("look('Lamplight', false, true); var power = h.hudButton(['interactive-btn', 'pm-hud-btn']); flush(); nextFrame();");

        Assert.Equal("power", Text("kindOf(power)"));
        Assert.Equal(1, Number("iconCount(power)"));
    }

    [Fact]
    public void A_button_added_inside_a_wrapper_later_is_dressed_in_the_next_frame()
    {
        Run($@"
            var wrapper = column.appendChild(page.createElement('div'));
            look('Lamplight', false, true);
            var lateBag = h.hudButton([], '{CoreUi0BagPath}', wrapper);
            flush();
            nextFrame();");

        Assert.Equal("bag", Text("kindOf(lateBag)"));
    }

    [Fact]
    public void Dressing_the_column_does_not_wake_its_own_watcher()
    {
        // The fake's flush throws if an observer keeps waking itself.
        Run("look('Lamplight', false, true); flush(); nextFrame(); nextFrame();");

        Assert.Equal(1, Number("iconCount(phone)"));
    }

    [Fact]
    public void One_icon_colour_marks_the_column_and_PerTool_takes_it_off()
    {
        Run("look('Lamplight', true, true);");
        Assert.True(ColumnHas("etk-mono"));

        Run("look('Lamplight', false, true);");
        Assert.False(ColumnHas("etk-mono"));
    }

    [Fact]
    public void Extras_off_takes_the_extras_class_off()
    {
        Run("look('Signal', false, false);");

        Assert.True(ColumnHas("etk-st-signal"));
        Assert.False(ColumnHas("etk-x"));
    }

    [Fact]
    public void One_icon_colour_is_amber_in_Lamplight_and_bone_in_Field_kit_and_Signal_keeps_its_colours()
    {
        var css = Css();

        Assert.Matches(@"\.vertical-tools\.etk-st-lamplight\.etk-mono,[^{]*\{--etk-hue:#f5b82e;", css);
        Assert.Matches(@"\.vertical-tools\.etk-st-fieldkit\.etk-mono,[^{]*\{--etk-hue:#f3ebd1;", css);
        Assert.DoesNotContain("etk-st-signal.etk-mono", css);
    }

    [Theory]
    [InlineData("trap", "#ee7a3e")]
    [InlineData("package", "#d9b27c")]
    [InlineData("guide", "#b9a2f0")]
    [InlineData("farm", "#8fd35f")]
    [InlineData("defense", "#6fb0ee")]
    [InlineData("machines", "#f2a03d")]
    [InlineData("power", "#f5dc4a")]
    [InlineData("storage", "#52cdb9")]
    [InlineData("phone", "#efe7d6")]
    [InlineData("bag", "#e0965a")]
    public void Each_tool_has_its_own_colour_in_every_style(string kind, string hue)
    {
        var css = Css();

        foreach (var style in new[] { "lamplight", "fieldkit", "signal" })
        {
            Assert.Contains($".vertical-tools.etk-st-{style} [data-etk-ic=\"{kind}\"]{{--etk-hue:{hue};", css);
        }
    }

    [Fact]
    public void The_icon_layers_are_mixed_the_same_in_the_page_and_in_the_previews()
    {
        // Field kit's dim layer for the trap: 40% of the hue with the rest olive.
        Assert.Contains("[data-etk-ic=\"trap\"]{--etk-hue:#ee7a3e;--etk-rgb:238,122,62;--etk-b:" + HudStyle.Mix("#ee7a3e", 0.4, "#8c875c") + ";", Css());
    }

    [Fact]
    public void Mix_takes_a_share_of_each_colour()
    {
        Assert.Equal("#808080", HudStyle.Mix("#ffffff", 0.5, "#000000"));
        Assert.Equal("#ee7a3e", HudStyle.Mix("#ee7a3e", 1, "#000000"));
    }

    [Fact]
    public void The_settings_default_to_Lamplight_with_a_colour_per_tool_and_extras_on()
    {
        Assert.Equal("Lamplight", HudStyle.DefaultStyle);
        Assert.Equal("PerTool", HudStyle.DefaultIconColours);
        Assert.True(HudStyle.DefaultExtras);
        Assert.Equal(new[] { "Lamplight", "FieldKit", "Signal", "Classic" }, HudStyle.Styles);
        Assert.Equal(new[] { "PerTool", "One" }, HudStyle.IconColourChoices);
    }

    [Theory]
    [InlineData("fieldkit", "FieldKit")]
    [InlineData(" Classic ", "Classic")]
    [InlineData("Neon", "Lamplight")]
    [InlineData("", "Lamplight")]
    [InlineData(null, "Lamplight")]
    public void A_saved_style_is_read_as_one_of_the_styles(string? saved, string expected)
    {
        Assert.Equal(expected, HudStyle.KnownStyle(saved!));
    }

    [Fact]
    public void The_script_hands_the_page_the_style_one_colour_and_extras()
    {
        Assert.Equal("window.__exiledtoolkitdock&&window.__exiledtoolkitdock.look&&window.__exiledtoolkitdock.look('Signal',true,false)", HudStyle.SetScript("signal", "one", false));
        Assert.Equal("window.__exiledtoolkitdock&&window.__exiledtoolkitdock.look&&window.__exiledtoolkitdock.look('Lamplight',false,true)", HudStyle.SetScript("x'); alert(1); ('", "nope", true));
    }

    [Fact]
    public void The_script_sets_the_look_in_the_page()
    {
        Run(HudStyle.SetScript(HudStyle.FieldKit, HudStyle.One, true) + ";");

        Assert.True(ColumnHas("etk-st-fieldkit"));
        Assert.True(ColumnHas("etk-mono"));
    }

    [Fact]
    public void Every_style_has_a_Chinese_name_and_one_preview()
    {
        var tags = HudStyle.StyleTags().Cast<string>().ToArray();

        foreach (var style in HudStyle.Styles)
        {
            Assert.Single(tags, tag => tag.StartsWith("ModManager.ChoiceZh=" + style + "=", StringComparison.Ordinal));
            Assert.Single(tags, tag => tag.StartsWith("ModManager.ChoicePreview=" + style + "=", StringComparison.Ordinal));
        }

        Assert.Contains("ModManager.ChoiceZh=One=统一颜色", HudStyle.IconColourTags.Cast<string>());
        Assert.Contains("ModManager.ChoiceZh=PerTool=按工具区分", HudStyle.IconColourTags.Cast<string>());
    }

    [Theory]
    [InlineData("Lamplight")]
    [InlineData("FieldKit")]
    [InlineData("Signal")]
    [InlineData("Classic")]
    public void A_preview_is_three_small_buttons_drawn_with_inline_styles_only(string style)
    {
        var html = HudStyle.Preview(style);

        Assert.Equal(3, Regex.Matches(html, "<svg ").Count);
        Assert.DoesNotContain("class=", html);
        Assert.DoesNotContain("<style", html);
        Assert.DoesNotContain("<script", html);
        Assert.True(html.Length <= 2048, $"{style} preview is {html.Length} characters");
    }

    [Fact]
    public void Each_preview_shows_its_own_style()
    {
        Assert.Contains("#ddd", HudStyle.Preview(HudStyle.Classic));
        Assert.Contains("M13 2L4 14h6l-1 8 9-12h-6z", HudStyle.Preview(HudStyle.Classic));
        Assert.Contains("M13.8 3.4L6.6 13.4", HudStyle.Preview(HudStyle.Lamplight));
        Assert.Contains("outline:1px dashed", HudStyle.Preview(HudStyle.FieldKit));
        Assert.Contains("inset 0 2px 0 #52cdb9", HudStyle.Preview(HudStyle.Signal));
        Assert.Equal(4, HudStyle.Styles.Select(HudStyle.Preview).Distinct().Count());
    }

    [Fact]
    public void Each_description_is_English_then_Chinese_and_names_its_default()
    {
        foreach (var description in new[] { HudStyle.StyleDescription, HudStyle.IconColoursDescription, HudStyle.ExtrasDescription })
        {
            var parts = description.Split('\n');

            Assert.Equal(2, parts.Length);
            Assert.Contains("Default:", parts[0]);
            Assert.Contains("默认", parts[1]);
        }
    }

    private int Paths(string kind) => kind is "trap" or "guide" ? 3 : 2;

    private static string Resource(string name)
    {
        using var stream = typeof(HudStyleTests).Assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException(name + " is not embedded in the test assembly");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd();
    }
}
