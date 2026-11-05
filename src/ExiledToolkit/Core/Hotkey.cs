using System;
using System.Collections.Generic;

namespace ExiledToolkit.Kit;

/// <summary>
/// When a mod's key fires: it is bound, it went down this frame, and no text box has the keyboard. A key that has a
/// button too starts unbound (<see cref="DefaultKey"/>), so no key is taken from the game or other mods. Toolkit
/// 0.19.0 and newer.
/// </summary>
public static class Hotkey
{
    /// <summary>The Unity key name of an unbound key setting.</summary>
    public const string DefaultKey = "None";

    /// <summary>
    /// Whether this frame's press fires the key. The press and the typing check are only asked for when they can change
    /// the answer: an unbound key asks nothing, and typing is only checked on a press.
    /// </summary>
    public static bool Fires(bool isBound, Func<bool> isPressed, Func<bool> isTyping) => isBound && isPressed() && !isTyping();
}

/// <summary>
/// The same rule as <see cref="Hotkey"/> for a key of any type, such as Unity's KeyCode, so a mod's tests need no Unity.
/// Toolkit 0.19.0 and newer.
/// </summary>
public static class KeyGate
{
    /// <summary>The Unity key name of an unbound key setting.</summary>
    public const string DefaultKeyName = Hotkey.DefaultKey;

    /// <summary>
    /// Asks <paramref name="isDown"/> only for a key that is not <paramref name="none"/>, and <paramref name="isTyping"/>
    /// only once the key went down, so an unset key costs nothing and the typing check runs on a press, not every frame.
    /// </summary>
    public static bool Fires<TKey>(TKey key, TKey none, Func<TKey, bool> isDown, Func<bool> isTyping) where TKey : struct =>
        !EqualityComparer<TKey>.Default.Equals(key, none) && isDown(key) && !isTyping();

    /// <param name="isTyping">Asked only for a press of a bound key.</param>
    public static bool Fires(bool isBound, bool isPressed, Func<bool> isTyping) => isBound && isPressed && !isTyping();
}
