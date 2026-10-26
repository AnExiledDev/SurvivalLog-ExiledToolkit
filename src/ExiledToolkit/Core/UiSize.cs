using System;
using System.Globalization;

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
        "window.ExiledUi&&window.ExiledUi.set(" + (Clamp(percent) / 100.0).ToString(CultureInfo.InvariantCulture) + ")";
}
