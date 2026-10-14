using System;
using System.Collections.Generic;
using System.Linq;
using ExiledToolkit.Core;

namespace ExiledToolkit;

/// <summary>
/// Toasts for any mod: short notices layered beside the game's HUD or in a screen corner. Declare a hard dependency on
/// <c>com.dev.survivallog.exiledtoolkit</c> and call <see cref="Show"/>.
/// </summary>
public static class Toasts
{
    internal static volatile ToastCenter? Center;

    /// <summary>
    /// Fires on the game's main thread for every toast trigger, shown or not, so a mod can keep a history. A trigger
    /// that merged into a toast still up carries that toast's id, the new count, and IsMerge true. A listener that
    /// throws is logged and does not stop the others.
    /// </summary>
    public static event Action<ToastRecord>? Raised;

    /// <summary>
    /// Shows a toast. Safe from any thread; it appears from the next frame. Returns the id the toast goes by: when an
    /// identical toast is still on screen or waiting, that toast's id, since the two merge into one with a count.
    /// Returns <see cref="ToastId.None"/> when the Toolkit has not started.
    /// </summary>
    public static ToastId Show(ToastRequest request) => Center?.Show(request) ?? ToastId.None;

    /// <summary>
    /// Runs a toast's click action, as if the player clicked it, on the game's main thread. Works after the toast has
    /// left the screen, for the most recent 500 toasts that have an action. False when the action is not known.
    /// </summary>
    public static bool Activate(ToastId id) => Center?.Activate(id) ?? false;

    internal static IReadOnlyList<Action<ToastRecord>> Listeners() =>
        Raised?.GetInvocationList().OfType<Action<ToastRecord>>().ToList() ?? new List<Action<ToastRecord>>();
}
