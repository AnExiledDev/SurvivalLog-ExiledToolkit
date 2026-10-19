using System;
using System.Collections.Generic;
using System.Linq;

namespace ExiledToolkit.Core;

/// <summary>
/// Settings a mod applies while the game runs. A change can arrive on any thread; every watcher of that setting runs
/// once on the main thread at the next <see cref="Run"/>, however many of its settings changed in between, so a
/// "reset all" costs each mod one apply.
/// </summary>
internal sealed class LiveQueue
{
    private readonly object _gate = new();
    private readonly List<LiveWatcher> _watchers = new();
    private readonly List<LiveWatcher> _due = new();

    /// <summary>Settings are compared by reference: each one is a single object for the life of the game.</summary>
    public LiveWatcher Watch(IEnumerable<object> settings, Action apply, string name)
    {
        var watcher = new LiveWatcher(this, settings.Distinct(ReferenceEqualityComparer.Instance).ToArray(), apply, name);

        lock (_gate) _watchers.Add(watcher);

        return watcher;
    }

    public bool IsWatched(object setting)
    {
        lock (_gate) return _watchers.Any(w => w.Covers(setting));
    }

    /// <summary>Safe from any thread. Only marks watchers due; nothing runs until the main thread calls <see cref="Run"/>.</summary>
    public void Changed(object setting)
    {
        lock (_gate)
        {
            foreach (var watcher in _watchers)
            {
                if (watcher.IsDue || !watcher.Covers(setting)) continue;

                watcher.IsDue = true;
                _due.Add(watcher);
            }
        }
    }

    /// <summary>
    /// Main thread only. Runs each due watcher once. A change made while they run waits for the next call, so an
    /// apply that writes its own setting cannot loop inside one frame. An apply that throws is logged and skipped.
    /// </summary>
    public void Run(Action<string> log)
    {
        LiveWatcher[] due;

        lock (_gate)
        {
            if (_due.Count == 0) return;

            due = _due.ToArray();
            _due.Clear();

            foreach (var watcher in due) watcher.IsDue = false;
        }

        foreach (var watcher in due) watcher.Apply(log);
    }

    internal void Remove(LiveWatcher watcher)
    {
        lock (_gate)
        {
            _watchers.Remove(watcher);
            _due.Remove(watcher);
        }
    }
}

/// <summary>One mod's apply and the settings it covers. Stopping it is final.</summary>
internal sealed class LiveWatcher : IDisposable
{
    private readonly LiveQueue _queue;
    private readonly object[] _settings;
    private readonly Action _apply;
    private volatile bool _isStopped;

    public LiveWatcher(LiveQueue queue, object[] settings, Action apply, string name)
    {
        _queue = queue;
        _settings = settings;
        _apply = apply;
        Name = name;
    }

    public string Name { get; }

    /// <summary>Guarded by the queue's lock.</summary>
    internal bool IsDue { get; set; }

    public bool Covers(object setting) => Array.IndexOf(_settings, setting) >= 0;

    /// <summary>Runs the apply now, on the calling thread, unless stopped. Never throws.</summary>
    public void Apply(Action<string> log)
    {
        if (_isStopped) return;

        try
        {
            _apply();
        }
        catch (Exception ex)
        {
            log("applying the live settings " + Name + " failed: " + ex);
        }
    }

    public void Dispose()
    {
        _isStopped = true;
        _queue.Remove(this);
    }
}
