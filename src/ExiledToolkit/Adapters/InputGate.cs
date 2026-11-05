namespace ExiledToolkit.Game;

/// <summary>
/// The game's keys and clicks around a mod window, in one place: while a window is open its hotkeys are held back
/// (<see cref="HotkeyGate"/>) and every click goes to the web UI (<see cref="ClickRouting"/>), since the game passes a
/// click to the web UI only where one of its own pages reported a button. A menu or popup that only needs the keys
/// held calls <see cref="HotkeyGate"/> under its own name. Main thread only. Toolkit 0.19.0 and newer.
/// </summary>
public static class InputGate
{
    /// <summary>Holds the hotkeys and captures every click under <paramref name="window"/>'s name. Opening twice is one open.</summary>
    public static void Opened(string window)
    {
        HotkeyGate.Hold(window);
        ClickRouting.Capture(window, true);
    }

    /// <summary>Lets both go. The hotkeys stay off through the next frame, so the Esc that closed the window does not also reach the game.</summary>
    public static void Closed(string window)
    {
        HotkeyGate.Release(window);
        ClickRouting.Capture(window, false);
    }
}
