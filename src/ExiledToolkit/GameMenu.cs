using System;
using System.Collections.Generic;
using ExiledToolkit.Core;

namespace ExiledToolkit;

/// <summary>Where a <see cref="GameMenuButton"/> goes in the game's own menus.</summary>
public enum GameMenuPlace
{
    /// <summary>The pause (Esc) menu, right after the game's Settings button.</summary>
    PauseTop,

    /// <summary>The pause (Esc) menu, at the bottom just above the game's Restart and Save &amp; Exit buttons.</summary>
    PauseBottom,

    /// <summary>The title screen, right before the game's Exit.</summary>
    Title,
}

/// <summary>
/// A button in the game's pause menu or on its title screen, drawn like the game's own. Id, label, place and click
/// action are required; the rest is optional. Chinese text left empty shows the English.
/// </summary>
public sealed class GameMenuButton
{
    public GameMenuButton(string id, string label, GameMenuPlace place, Action onClick)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Label = label ?? throw new ArgumentNullException(nameof(label));
        Place = place;
        OnClick = onClick ?? throw new ArgumentNullException(nameof(onClick));
    }

    /// <summary>Unique across mods: 1 to 64 letters, digits, '.', '_' or '-'. Use "yourmod.action".</summary>
    public string Id { get; }

    public string Label { get; }

    public string ChineseLabel { get; init; } = "";

    public GameMenuPlace Place { get; }

    /// <summary>Buttons in one place sort by this, then by id.</summary>
    public int Order { get; init; } = 500;

    /// <summary>The colour of the button's left edge (the bar, on the title screen) as #rgb or #rrggbb; empty keeps the game's.</summary>
    public string Accent { get; init; } = "";

    /// <summary>
    /// Asked on the main thread a few times a second; the button shows only while it returns true. Null shows it
    /// always. One that throws counts as false and is logged once.
    /// </summary>
    public Func<bool>? IsShown { get; init; }

    /// <summary>Runs on the game's main thread after the player clicks the button. The menu stays open.</summary>
    public Action OnClick { get; }
}

/// <summary>
/// Buttons in the game's pause menu and title screen, shared by every mod. The Toolkit places them, keeps them there
/// when the game redraws its menu, and shrinks the whole menu's buttons and text to fit the screen however many are
/// added. Declare a hard dependency on <c>com.dev.survivallog.exiledtoolkit</c> and call <see cref="Add"/> once, say
/// from your plugin's Load.
/// </summary>
public static class GameMenu
{
    private static readonly object Gate = new();
    private static readonly GameMenuBoard Board = new();
    private static readonly Dictionary<string, GameMenuButton> Buttons = new(StringComparer.Ordinal);

    /// <summary>Adds the button, or replaces the one with its id. Safe from any thread; the page has it within a second.</summary>
    public static void Add(GameMenuButton button)
    {
        if (button == null) throw new ArgumentNullException(nameof(button));

        lock (Gate)
        {
            Board.Put(new GameMenuItem(button.Id, button.Label, button.ChineseLabel, Spot(button.Place), button.Order, button.Accent ?? ""));
            Buttons[button.Id] = button;
        }
    }

    public static bool Remove(string id)
    {
        lock (Gate)
        {
            Buttons.Remove(id);

            return Board.Remove(id);
        }
    }

    internal static GameMenuButton? Find(string id)
    {
        lock (Gate)
        {
            return Buttons.TryGetValue(id, out var button) ? button : null;
        }
    }

    /// <summary>Under the lock, so an Add from another thread cannot change the board mid-read.</summary>
    internal static string? Pending(bool isChinese, Func<GameMenuButton, bool> isShown)
    {
        lock (Gate)
        {
            return Board.Pending(isChinese, id => Buttons.TryGetValue(id, out var button) && isShown(button));
        }
    }

    internal static void Shown(string script)
    {
        lock (Gate)
        {
            Board.Shown(script);
        }
    }

    internal static void PageReset()
    {
        lock (Gate)
        {
            Board.PageReset();
        }
    }

    private static string Spot(GameMenuPlace place) => place switch
    {
        GameMenuPlace.PauseTop => GameMenuSpot.PauseTop,
        GameMenuPlace.PauseBottom => GameMenuSpot.PauseBottom,
        GameMenuPlace.Title => GameMenuSpot.Title,
        _ => throw new ArgumentOutOfRangeException(nameof(place), place, "unknown menu place"),
    };
}
