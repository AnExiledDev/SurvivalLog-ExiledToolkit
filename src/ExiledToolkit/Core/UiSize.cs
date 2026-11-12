using System;
using System.Globalization;
using System.Text;

namespace ExiledToolkit.Core;

/// <summary>
/// The size mods draw their own windows and panels at, in percent of the size they were made for (Interface &gt;
/// UiScale). The page side is ui.js, which zooms every panel a mod hands it.
/// </summary>
internal static class UiSize
{
    public const int Min = 75;
    public const int Max = 200;
    public const int Default = 100;

    public static int Clamp(int percent) => Math.Clamp(percent, Min, Max);

    /// <summary>Sets the size in the page; does nothing while ui.js is not there, as just after the web view reloads.</summary>
    public static string SetScript(int percent) =>
        "window.ExiledUi&&window.ExiledUi.set(" + Factor(Clamp(percent)) + ")";

    /// <summary>
    /// Sets one mod's own size in the page, or with 0 has its panels follow the player's size again. An older ui.js
    /// without per-mod sizes ignores it.
    /// </summary>
    public static string ModScript(string mod, int percent) =>
        "window.ExiledUi&&window.ExiledUi.mod&&window.ExiledUi.mod(\"" + CheckedName(mod) + "\"," + (percent > 0 ? Factor(Clamp(percent)) : "0") + ")";

    /// <summary>A mod's name as it hands its panels over: letters, digits, dots, dashes and underscores.</summary>
    public static string CheckedName(string mod)
    {
        if (string.IsNullOrEmpty(mod)) throw new ArgumentException("a mod name is needed", nameof(mod));

        foreach (var c in mod)
        {
            if (!(c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '.' or '-' or '_')) throw new ArgumentException("a mod name may hold only letters, digits, dots, dashes and underscores: " + mod, nameof(mod));
        }

        return mod;
    }

    /// <summary>A mod's name as players read it: "FarmHub" is "Farm Hub", with a space before each capital that follows a small letter or digit.</summary>
    public static string DisplayName(string mod)
    {
        var name = new StringBuilder(mod.Length + 4);

        for (var i = 0; i < mod.Length; i++)
        {
            if (i > 0 && char.IsUpper(mod[i]) && char.IsLetterOrDigit(mod[i - 1]) && !char.IsUpper(mod[i - 1])) name.Append(' ');

            name.Append(mod[i]);
        }

        return name.ToString();
    }

    /// <summary>The description of a mod's own Interface &gt; UiScale setting, English then Chinese.</summary>
    public static string ModDescription(string displayName) =>
        $"How big {displayName}'s windows, panels and messages are, in percent, from {Min} to {Max}. Set it to 0 to use the UiScale setting from Exiled's Toolkit instead.\n" +
        $"{displayName} 的窗口、面板和提示的大小（百分比），范围 {Min} 到 {Max}。设为 0 时改用 Exiled's Toolkit 的 UiScale 设置。";

    private static string Factor(int percent) => (percent / 100.0).ToString(CultureInfo.InvariantCulture);
}
