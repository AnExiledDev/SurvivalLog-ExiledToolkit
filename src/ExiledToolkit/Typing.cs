using ExiledToolkit.Adapters;

namespace ExiledToolkit;

/// <summary>
/// Whether the player is typing in a text box, in any page or mod panel. The game's text boxes count too, such as the
/// item search and a storage's name. Toolkit 0.12.0 and newer.
/// </summary>
public static class Typing
{
    /// <summary>
    /// True while a text box has the keyboard. A mod skips its own hotkeys while this is true. Read it on the game's main
    /// thread. With an older Toolkit the read throws <c>MissingMethodException</c>.
    /// </summary>
    public static bool IsActive => TypingGate.IsActive;
}
