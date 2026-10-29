using System;
using ExiledToolkit.Adapters;
using ExiledToolkit.Core;

namespace ExiledToolkit;

/// <summary>
/// A mod's own UI size, which overrides the player's Interface &gt; UiScale for the panels the mod hands to
/// <c>ExiledUi.size</c> with <c>{ mod: name }</c>. Toolkit 0.11.0 and newer.
/// </summary>
public static class ModUiSize
{
    /// <summary>
    /// Draws the mod's panels at <paramref name="percent"/> (75 to 200; a value outside is drawn at the nearest end),
    /// or with 0 or less at the player's size again. <paramref name="mod"/> is the name the page script passes as
    /// <c>mod</c>: letters, digits, dots, dashes and underscores. Call it on the game's main thread, as
    /// <see cref="LiveSettings"/> does.
    /// </summary>
    public static void Set(string mod, int percent)
    {
        UiSize.CheckedName(mod);

        var ui = ToastTicker.Ui ?? throw new InvalidOperationException("Exiled's Toolkit has not loaded yet");

        ui.SetMod(mod, percent);
        ToastTicker.Log(percent > 0 ? $"{mod} windows drawn at {UiSize.Clamp(percent)}%" : $"{mod} windows follow UiScale");
    }
}
