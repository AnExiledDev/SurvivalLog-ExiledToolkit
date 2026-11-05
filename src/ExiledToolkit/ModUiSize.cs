using System;
using BepInEx.Configuration;
using ExiledToolkit.Adapters;
using ExiledToolkit.Core;

namespace ExiledToolkit;

/// <summary>
/// A mod's own UI size, which overrides the player's Interface &gt; UiScale for the panels the mod hands to
/// <c>ExiledUi.size</c> with <c>{ mod: name }</c>. Toolkit 0.11.0 and newer; <see cref="Bind"/> 0.19.0 and newer.
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

    /// <summary>
    /// Binds the mod's Interface &gt; UiScale setting (0 to 200, default 0: follow the player's UiScale) with the standard
    /// description, and applies it live through <see cref="Set"/>. Call it once from the plugin's Load.
    /// </summary>
    /// <param name="mod">The name the page script passes as <c>mod</c>, such as "FarmHub".</param>
    /// <param name="displayName">The mod's name in the description; null splits <paramref name="mod"/> ("Farm Hub").</param>
    /// <param name="description">A description of your own, English then "\n" then Chinese, for a mod whose panels are not windows.</param>
    public static ConfigEntry<int> Bind(ConfigFile config, string mod, string? displayName = null, string? description = null)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        UiSize.CheckedName(mod);

        var entry = config.Bind("Interface", "UiScale", 0, new ConfigDescription(
            description ?? UiSize.ModDescription(displayName ?? UiSize.DisplayName(mod)),
            new AcceptableValueRange<int>(0, UiSize.Max)));
        var hasSent = false;

        LiveSettings.Watch(entry, percent =>
        {
            // 0 at start is what the Toolkit has anyway, so nothing is sent until there is a size to send.
            if (percent <= 0 && !hasSent) return;

            Set(mod, percent);
            hasSent = true;
        });

        return entry;
    }
}
