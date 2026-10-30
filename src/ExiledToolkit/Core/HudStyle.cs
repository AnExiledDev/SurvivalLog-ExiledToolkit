using System;
using System.Text;

namespace ExiledToolkit.Core;

/// <summary>
/// How the HUD's tool column is drawn (Interface &gt; HudButtonStyle, HudIconColours, HudButtonExtras): the settings'
/// choices, their descriptions and Mod Manager tags, and the call that hands them to dock.js.
/// </summary>
internal static class HudStyle
{
    public const string Lamplight = "Lamplight";
    public const string FieldKit = "FieldKit";
    public const string Signal = "Signal";
    public const string Classic = "Classic";
    public static readonly string[] Styles = { Lamplight, FieldKit, Signal, Classic };
    public const string DefaultStyle = Lamplight;

    public const string PerTool = "PerTool";
    public const string One = "One";
    public static readonly string[] IconColourChoices = { PerTool, One };
    public const string DefaultIconColours = PerTool;

    public const bool DefaultExtras = true;

    public const string StyleDescription =
        "How the buttons in the HUD's tool column look: the buttons mods add there and the game's own phone and bag buttons. " +
        "Lamplight: dark brown buttons with each tool's icon in its own colour. " +
        "FieldKit: olive canvas buttons with stitching that press down like keys. " +
        "Signal: dark buttons with each tool's icon lit in its own colour. " +
        "Classic: the game's normal grey buttons and icons. A change shows at once. Default: Lamplight.\n" +
        "HUD 工具栏中按钮的样式：包括模组添加的按钮，以及游戏自带的手机和背包按钮。" +
        "灯火：深棕色按钮，每个工具的图标各有颜色。" +
        "野外装备：带缝线的橄榄色帆布按钮，按下时像键盘按键一样下沉。" +
        "信号灯：深色按钮，每个工具的图标以各自的颜色发光。" +
        "经典：游戏原本的灰色按钮和图标。改动会立即生效。默认「灯火」。";

    public const string IconColoursDescription =
        "PerTool: each tool's icon has its own colour, so you can find a tool by its colour. " +
        "One: every icon is amber in Lamplight and bone white in FieldKit. " +
        "Signal always gives each tool its own colour, and Classic keeps the game's icons. A change shows at once. Default: PerTool.\n" +
        "按工具区分：每个工具的图标各有颜色，可以凭颜色找到工具。" +
        "统一颜色：「灯火」中所有图标为琥珀色，「野外装备」中为骨白色。" +
        "「信号灯」始终按工具区分颜色，「经典」保留游戏原本的图标。改动会立即生效。默认「按工具区分」。";

    public const string ExtrasDescription =
        "Adds the touches of the FieldKit style to Lamplight and Signal: buttons that press down like keys, a striped edge on a button that needs you, stamped number badges and paper tags for the tips. " +
        "FieldKit always has them and Classic never does. A change shows at once. Default: on.\n" +
        "为「灯火」和「信号灯」加上「野外装备」样式的细节：按下时像键盘按键一样下沉的按钮、需要你处理的按钮底部的条纹、盖章样式的数字角标，以及纸签样式的提示。" +
        "「野外装备」始终带有这些细节，「经典」从不带有。改动会立即生效。默认开启。";

    public static readonly object[] IconColourTags =
    {
        "ModManager.ChoiceZh=PerTool=按工具区分",
        "ModManager.ChoiceZh=One=统一颜色",
    };

    public static object[] StyleTags()
    {
        return new object[]
        {
            "ModManager.ChoiceZh=Lamplight=灯火",
            "ModManager.ChoiceZh=FieldKit=野外装备",
            "ModManager.ChoiceZh=Signal=信号灯",
            "ModManager.ChoiceZh=Classic=经典",
            "ModManager.ChoicePreview=Lamplight=" + Preview(Lamplight),
            "ModManager.ChoicePreview=FieldKit=" + Preview(FieldKit),
            "ModManager.ChoicePreview=Signal=" + Preview(Signal),
            "ModManager.ChoicePreview=Classic=" + Preview(Classic),
        };
    }

    /// <summary>A saved value as one of the styles; anything else is the default.</summary>
    public static string KnownStyle(string value) => Known(value, Styles, DefaultStyle);

    public static string KnownIconColours(string value) => Known(value, IconColourChoices, DefaultIconColours);

    private static string Known(string value, string[] choices, string fallback)
    {
        foreach (var choice in choices)
        {
            if (string.Equals(choice, value?.Trim(), StringComparison.OrdinalIgnoreCase)) return choice;
        }

        return fallback;
    }

    /// <summary>Sets the look in the page; does nothing while dock.js is not there, as just after the web view reloads.</summary>
    public static string SetScript(string style, string iconColours, bool extras) =>
        "window.__exiledtoolkitdock&&window.__exiledtoolkitdock.look&&window.__exiledtoolkitdock.look('" +
        KnownStyle(style) + "'," + (KnownIconColours(iconColours) == One ? "true" : "false") + "," + (extras ? "true" : "false") + ")";

    // The three preview buttons: the icon in each style (dim and lit layers, the game's own icon for Classic) and the
    // tool's colour.
    private static readonly (string Hue, string Now, string[] Layers)[] PreviewTools =
    {
        ("#ee7a3e", "M3 3h18v3H3V3zm2 4h2v10H5V7zm5 0h2v10h-2V7zm5 0h2v10h-2V7zm4 0h2v10h-2V7zM3 18h18v3H3v-3z", new[]
        {
            "bM2 1.8h20v1.9H2zM2 20.1h20v2.1H2zM2.6 3.7h1.6v16.4H2.6zM19.8 3.7h1.6v16.4h-1.6z",
            "fM7 16.2a5 4 0 1 0 10 0a5 4 0 1 0-10 0zM9 11.2a3 3 0 1 0 6 0a3 3 0 1 0-6 0zM9.4 6.6a1.1 3 0 1 0 2.2 0a1.1 3 0 1 0-2.2 0zM12.4 6.6a1.1 3 0 1 0 2.2 0a1.1 3 0 1 0-2.2 0z",
            "bM6.6 3.7h1.1v16.4H6.6zM16.3 3.7h1.1v16.4h-1.1z",
        }),
        ("#f5dc4a", "M13 2L4 14h6l-1 8 9-12h-6z", new[]
        {
            "bM2 12a10 10 0 1 0 20 0a10 10 0 1 0-20 0z",
            "fM13.8 3.4L6.6 13.4h4.8l-1.3 7.2 7.3-10.2h-4.9z",
        }),
        ("#52cdb9", "M4 4h16a1 1 0 0 1 1 1v4H3V5a1 1 0 0 1 1-1zm-1 7h7v2h4v-2h7v8a1 1 0 0 1-1 1H4a1 1 0 0 1-1-1z", new[]
        {
            "bM2.6 11.4h18.8v9a1.2 1.2 0 0 1-1.2 1.2H3.8a1.2 1.2 0 0 1-1.2-1.2zM2.6 10.2V8.2A5 5 0 0 1 7.6 3.2h8.8a5 5 0 0 1 5 5v2z",
            "fM5.8 3.6h2v18H5.8zM16.2 3.6h2v18h-2zM10.2 8.4h3.6v5.4h-3.6z",
        }),
    };

    /// <summary>
    /// Three buttons (traps, power, storage) in a style, for Mod Manager's choice list. Inline styles and svg only: it
    /// is drawn in Mod Manager's page, which has none of the dock's stylesheet.
    /// </summary>
    public static string Preview(string style)
    {
        style = KnownStyle(style);
        var html = new StringBuilder("<div style=\"display:inline-flex;gap:5px;padding:5px;border-radius:5px;background:#2a1f14\">");

        foreach (var (hue, now, layers) in PreviewTools)
        {
            html.Append("<div style=\"width:34px;height:34px;display:flex;align-items:center;justify-content:center;box-sizing:border-box;")
                .Append(Plate(style, hue))
                .Append("\"><svg viewBox=\"0 0 24 24\" width=\"20\" height=\"20\">");

            if (style == Classic)
            {
                html.Append("<path fill=\"#ddd\" d=\"").Append(now).Append("\"/>");
            }
            else
            {
                foreach (var layer in layers)
                {
                    html.Append("<path fill=\"").Append(layer[0] == 'b' ? Dim(style, hue) : Lit(style, hue))
                        .Append("\" d=\"").Append(layer, 1, layer.Length - 1).Append("\"/>");
                }
            }

            html.Append("</svg></div>");
        }

        return html.Append("</div>").ToString();
    }

    private static string Plate(string style, string hue) => style switch
    {
        Lamplight => "border-radius:5px;background:linear-gradient(#34291c,#1c1510);border:1px solid #6b5020",
        FieldKit => "border-radius:3px;background:linear-gradient(#5b5a3c,#3e3d28);border:1px solid #1f1e15;outline:1px dashed #a5a088;outline-offset:-4px",
        Signal => "border-radius:6px;background:linear-gradient(#24201b,#15120e);border:1px solid #2c2925;box-shadow:inset 0 2px 0 " + hue,
        _ => "border-radius:4px;background:#3c3c3c;border:1px solid #5d5d5d",
    };

    private static string Dim(string style, string hue) => style switch
    {
        Lamplight => Mix(hue, 0.52, "#1c1510"),
        FieldKit => Mix(hue, 0.40, "#8c875c"),
        _ => Mix(hue, 0.58, "#15120e"),
    };

    private static string Lit(string style, string hue) => style == FieldKit ? Mix(hue, 0.62, "#f3ebd1") : hue;

    /// <summary>share of a with the rest of b, both "#rrggbb", as dock.js mixes the icon colours.</summary>
    public static string Mix(string a, double share, string b)
    {
        var mixed = new StringBuilder("#");

        for (var i = 1; i < 7; i += 2)
        {
            var x = Convert.ToInt32(a.Substring(i, 2), 16);
            var y = Convert.ToInt32(b.Substring(i, 2), 16);
            mixed.Append(((int)Math.Round(x * share + y * (1 - share), MidpointRounding.AwayFromZero)).ToString("x2"));
        }

        return mixed.ToString();
    }
}
