using System;
using System.Collections.Generic;

namespace ExiledToolkit.Game;

/// <summary>
/// How often each kind of failure from a hook has been written. A hook the game calls every frame would flood the log,
/// so only the first few of each kind are; one failing every frame cannot hide a different one failing later. Any thread.
/// </summary>
internal sealed class FailureCounts
{
    private readonly Dictionary<string, int> _byDoing = new();

    public FailureCounts(int maxLogged)
    {
        if (maxLogged < 1) throw new ArgumentOutOfRangeException(nameof(maxLogged), maxLogged, "at least one failure of each kind is logged");

        MaxLogged = maxLogged;
    }

    public int MaxLogged { get; }

    /// <summary>The line to log for this failure, or null once enough of its kind were. Built only when it is logged.</summary>
    public string? LineFor(string doing, Exception ex)
    {
        int failures;

        lock (_byDoing)
        {
            failures = _byDoing.TryGetValue(doing, out var before) ? before + 1 : 1;

            if (failures > MaxLogged) return null;

            _byDoing[doing] = failures;
        }

        return MaxLogged == 1 ? $"failed {doing} (reported once): {ex}" : $"failed {doing} (logged {failures} of {MaxLogged}): {ex}";
    }
}
