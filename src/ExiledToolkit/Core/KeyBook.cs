using System;
using System.Collections.Generic;
using System.Threading;

namespace ExiledToolkit.Core;

/// <summary>
/// The game's key for each of its actions, read once and kept. Asking the game costs a trip into its key settings,
/// and a mod may ask about a key every frame one is held. The game only changes a key as it saves its settings, so
/// the adapter calls <see cref="Forget"/> then. Until those saves are watched (<see cref="IsWatched"/>), every ask
/// reads the game again. A read that comes back null (the game has not made its settings yet) is not kept.
/// </summary>
internal sealed class KeyBook
{
    private readonly Dictionary<int, int> _known = new();
    private readonly Func<int, int?> _read;
    private int _version;
    private int _knownVersion;

    /// <param name="read">Reads the game's key for an action, both as plain numbers, or null when it cannot yet.</param>
    public KeyBook(Func<int, int?> read) => _read = read;

    /// <summary>Whether the game's settings saves are watched, so a kept key can be trusted.</summary>
    public bool IsWatched { get; set; }

    /// <summary>Main thread: a key not kept is read from the game.</summary>
    public int? KeyFor(int action)
    {
        if (!IsWatched) return _read(action);

        var version = Volatile.Read(ref _version);

        if (_knownVersion != version)
        {
            _known.Clear();
            _knownVersion = version;
        }

        if (_known.TryGetValue(action, out var kept)) return kept;

        var key = _read(action);

        if (key is { } found) _known[action] = found;

        return key;
    }

    /// <summary>Safe from any thread: the keys kept are dropped on the next ask.</summary>
    public void Forget() => Interlocked.Increment(ref _version);
}
