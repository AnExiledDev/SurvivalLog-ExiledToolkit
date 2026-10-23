using System;
using System.Collections.Generic;
using System.Reflection;

namespace ExiledToolkit.Core;

/// <summary>The player's toast settings, read at start and again whenever one of them changes.</summary>
internal sealed record ToastSettings(ToastPosition Position, double DefaultSeconds, int MaxVisible, bool IsEnabled)
{
    /// <summary>Beside the HUD, unless the player picks a corner.</summary>
    public const ToastPosition DefaultPosition = ToastPosition.Hud;

    /// <summary>Used when the setting itself is not a number.</summary>
    public const double FallbackSeconds = 7;
    public const double MinSeconds = 1;
    public const double MaxSeconds = 120;

    /// <summary>The deck shows a front toast and three behind it (toasts.js); more would sit unseen, so they wait.</summary>
    public const int MaxMaxVisible = 4;

    /// <summary>The config file can hold a number for the position; one that names no position is the default.</summary>
    public static ToastPosition KnownPosition(ToastPosition position) =>
        Enum.IsDefined(typeof(ToastPosition), position) ? position : DefaultPosition;

    /// <summary>How long a toast stays up: its own time if it gave a usable one, else the setting, kept in range.</summary>
    public int Milliseconds(double? requested)
    {
        var seconds = requested is { } wanted && double.IsFinite(wanted) ? wanted : DefaultSeconds;

        if (!double.IsFinite(seconds)) seconds = FallbackSeconds;

        return (int)Math.Round(Math.Clamp(seconds, MinSeconds, MaxSeconds) * 1000);
    }
}

/// <summary>The two clocks a toast needs: one that only moves forward, for timing, and the wall clock, for history.</summary>
internal sealed record ToastClock(Func<long> Milliseconds, Func<DateTime> UtcNow);

/// <summary>
/// The toast service behind <see cref="Toasts"/>. Show may be called from any thread and decides under a lock, so it
/// can answer with the id the toast really goes by, merges included. Everything that reaches a mod or the page waits
/// for <see cref="Pump"/> on the game's main thread: listeners, click actions asked for off that thread, and the page
/// ops of the frame, sent as one script call.
/// </summary>
internal sealed class ToastCenter
{
    private readonly object _gate = new();
    private ToastSettings _settings;
    private readonly ToastClock _clock;
    private readonly Func<bool> _isMainThread;
    private readonly Action<string> _log;
    private readonly ToastBoard _board;
    private readonly ClickActions _actions = new();
    private readonly List<PageOp> _ops = new();
    private readonly Queue<ToastRecord> _raised = new();
    private readonly Queue<ToastId> _activations = new();

    /// <summary>Touched by <see cref="Pump"/> only, on the main thread.</summary>
    private readonly Dictionary<(MethodInfo Listener, Type Exception), int> _listenerFailures = new();
    private long _lastId;

    public ToastCenter(ToastSettings settings, ToastClock clock, Func<bool> isMainThread, Action<string> log)
    {
        _settings = settings;
        _clock = clock;
        _isMainThread = isMainThread;
        _log = log;
        _board = new ToastBoard(Math.Clamp(settings.MaxVisible, 1, ToastSettings.MaxMaxVisible));
    }

    /// <summary>
    /// Takes changed settings while the game runs. A new position moves the stack at the next frame; a new on-screen
    /// limit shows waiting toasts at once or lets extra ones run out; the time and the off switch count from the next toast.
    /// </summary>
    public void Configure(ToastSettings settings)
    {
        lock (_gate)
        {
            if (settings.Position != _settings.Position) _ops.Add(new PageOp.Place(settings.Position));

            _settings = settings;
            _board.Resize(Math.Clamp(settings.MaxVisible, 1, ToastSettings.MaxMaxVisible), _clock.Milliseconds(), _ops);
        }
    }

    public ToastId Show(ToastRequest? request)
    {
        if (request == null)
        {
            _log("a mod asked for a toast without saying what it is; nothing was shown");

            return ToastId.None;
        }

        var content = new ToastContent(request.Source ?? "", request.Title ?? "", Optional(request.Body), request.Kind, Optional(request.Icon))
        {
            Items = ToastItems.From(request.Items),
        };
        var raisedAt = _clock.UtcNow();

        lock (_gate)
        {
            var placed = Place(content, request);

            _actions.Set(placed.Id, request.OnClick);
            _raised.Enqueue(new ToastRecord(placed.Id, content.Source, content.Title, content.Body, content.Kind, content.Icon, raisedAt, request.OnClick != null, placed.Count, placed.IsMerge));

            return placed.Id;
        }
    }

    /// <summary>
    /// Runs the toast's click action, on the main thread: now when called there, else at the next frame. False when the
    /// toast is unknown or too old for its action to be kept.
    /// </summary>
    public bool Activate(ToastId id)
    {
        Action action;

        lock (_gate)
        {
            if (!_actions.TryGet(id, out action)) return false;

            if (!_isMainThread())
            {
                _activations.Enqueue(id);

                return true;
            }
        }

        RunAction(id, action);

        return true;
    }

    /// <summary>The page says the box left the screen showing <paramref name="shownCount"/>.</summary>
    public void Gone(ToastId id, int shownCount, bool isClosedByPlayer)
    {
        lock (_gate) _board.Dismiss(id, shownCount, isClosedByPlayer, _clock.Milliseconds(), _ops);
    }

    /// <summary>The page script was just installed: it gets its position and every toast that should be on screen.</summary>
    public void PageReady()
    {
        lock (_gate)
        {
            _ops.Add(new PageOp.Place(_settings.Position));
            _board.Reshow(_clock.Milliseconds(), _ops);
        }
    }

    /// <summary>
    /// Main thread, once a frame: tells each listener about every toast raised since the last frame, runs click actions
    /// asked for from other threads, and hands back the page ops to send.
    /// </summary>
    public IReadOnlyList<PageOp> Pump(IReadOnlyList<Action<ToastRecord>> listeners)
    {
        ToastRecord[] raised;
        ToastId[] activations;
        IReadOnlyList<PageOp> ops;

        lock (_gate)
        {
            var now = _clock.Milliseconds();

            _board.SendDueMerges(now, _ops);
            _board.Expire(now, _ops);

            raised = _raised.ToArray();
            activations = _activations.ToArray();
            ops = PageOp.Coalesce(_ops);

            _raised.Clear();
            _activations.Clear();
            _ops.Clear();
        }

        foreach (var record in raised) Raise(record, listeners);

        foreach (var id in activations) RunIfKept(id);

        return ops;
    }

    private Placed Place(ToastContent content, ToastRequest request)
    {
        var id = new ToastId(++_lastId);

        if (!_settings.IsEnabled) return new Placed(id, 1, false);

        return _board.Add(id, content, Optional(request.Key), _settings.Milliseconds(request.Seconds), request.OnClick != null, _clock.Milliseconds(), _ops);
    }

    /// <summary>One listener that throws must not keep the others from hearing about the toast.</summary>
    private void Raise(ToastRecord record, IReadOnlyList<Action<ToastRecord>> listeners)
    {
        foreach (var listener in listeners)
        {
            try
            {
                listener(record);
            }
            catch (Exception ex)
            {
                LogListenerFailure(listener, record, ex);
            }
        }
    }

    /// <summary>
    /// The first failure of a listener with an exception type is logged in full; repeats are counted and the count is
    /// logged at 10, 100, 1000 and so on, so a listener that fails on every toast of a burst cannot flood the log.
    /// </summary>
    private void LogListenerFailure(Action<ToastRecord> listener, ToastRecord record, Exception ex)
    {
        var key = (listener.Method, ex.GetType());
        var failures = _listenerFailures.TryGetValue(key, out var earlier) ? earlier + 1 : 1;
        var name = listener.Method.DeclaringType?.FullName + "." + listener.Method.Name;
        _listenerFailures[key] = failures;

        if (failures == 1)
        {
            _log($"a mod listening for toasts ({name}) failed on \"{record.Title}\"; repeats are counted, not logged in full: {ex}");
        }
        else if (IsPowerOfTen(failures))
        {
            _log($"a mod listening for toasts ({name}) has now failed {failures} times with {ex.GetType().Name}");
        }
    }

    private static bool IsPowerOfTen(int value)
    {
        while (value >= 10 && value % 10 == 0) value /= 10;

        return value == 1;
    }

    /// <summary>An action asked for from another thread may have been dropped from the kept ones before this frame.</summary>
    private void RunIfKept(ToastId id)
    {
        Action action;

        lock (_gate)
        {
            if (!_actions.TryGet(id, out action)) return;
        }

        RunAction(id, action);
    }

    private void RunAction(ToastId id, Action action)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            _log($"the click action of toast {id.Value} failed: {ex}");
        }
    }

    private static string? Optional(string? text) => string.IsNullOrEmpty(text) ? null : text;
}
