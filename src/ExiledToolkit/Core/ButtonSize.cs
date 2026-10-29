using System;
using System.Globalization;

namespace ExiledToolkit.Core;

/// <summary>
/// The size the HUD's tool column is drawn at, mods' buttons and the game's own alike, in percent of the game's size
/// (Interface &gt; HudButtonSize). The page side is dock.js.
/// </summary>
internal static class ButtonSize
{
    public const int Min = 50;
    public const int Max = 150;
    public const int Default = 80;

    public static int Clamp(int percent) => Math.Clamp(percent, Min, Max);

    /// <summary>Sets the size in the page; does nothing while dock.js is not there, as just after the web view reloads.</summary>
    public static string SetScript(int percent) =>
        "window.__exiledtoolkitdock&&window.__exiledtoolkitdock.size&&window.__exiledtoolkitdock.size(" +
        (Clamp(percent) / 100.0).ToString(CultureInfo.InvariantCulture) + ")";
}
