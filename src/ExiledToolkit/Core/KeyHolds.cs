using System;
using System.Collections.Generic;

namespace ExiledToolkit.Game;

/// <summary>
/// Who is holding the game's hotkeys back, by name, and the frame they stay held through after the last one lets go,
/// so the key that closed a window (Esc) is not seen by the game too. The frame is asked for only when it can change
/// the answer, since reading it is a call into the engine. Main thread only.
/// </summary>
internal sealed class KeyHolds
{
    private readonly HashSet<string> _holders = new();
    private int _heldThroughFrame = -1;

    public bool IsHeld => _holders.Count > 0;

    public bool IsHeldBy(string holder) => _holders.Contains(holder);

    public void Hold(string holder) => _holders.Add(holder ?? throw new ArgumentNullException(nameof(holder)));

    /// <summary>A holder that was not holding changes nothing.</summary>
    public void Release(string holder, Func<int> frame)
    {
        if (!_holders.Remove(holder ?? throw new ArgumentNullException(nameof(holder)))) return;

        _heldThroughFrame = frame() + 1;
    }

    /// <summary>Whether the game's hotkeys run this frame.</summary>
    public bool LetsGameRun(Func<int> frame)
    {
        if (_holders.Count > 0) return false;

        if (_heldThroughFrame < 0) return true;

        if (frame() <= _heldThroughFrame) return false;

        _heldThroughFrame = -1;

        return true;
    }
}
