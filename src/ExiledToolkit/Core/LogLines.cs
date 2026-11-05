using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ExiledToolkit.Kit;

/// <summary>
/// Log lines waiting for the file. Adding one only stores the message and the time, so the thread that logs never
/// waits on the disk; the date stamp and the text for the file are built by whoever takes them, on the writer's
/// thread. Any thread may add; one taker at a time. Toolkit 0.19.0 and newer.
/// </summary>
public sealed class LogLines
{
    public const string StampFormat = "yyyy-MM-dd HH:mm:ss.fff";

    private readonly object _gate = new();
    private readonly StringBuilder _batch = new();
    private List<(DateTime AtUtc, string Message)> _pending = new();
    private List<(DateTime AtUtc, string Message)> _taking = new();

    /// <summary>Queues a line. True when it is the first line since the last batch was taken, so the writer needs waking.</summary>
    public bool Add(DateTime atUtc, string message)
    {
        lock (_gate)
        {
            _pending.Add((atUtc, message));

            return _pending.Count == 1;
        }
    }

    public bool IsEmpty
    {
        get
        {
            lock (_gate) return _pending.Count == 0;
        }
    }

    /// <summary>Every line waiting, oldest first, each stamped with its local time and ending in a line break; null when none are waiting.</summary>
    public string? Take()
    {
        lock (_gate)
        {
            if (_pending.Count == 0) return null;

            (_pending, _taking) = (_taking, _pending);
        }

        _batch.Clear();

        foreach (var (atUtc, message) in _taking)
        {
            _batch.Append(atUtc.ToLocalTime().ToString(StampFormat, CultureInfo.InvariantCulture)).Append(' ').Append(message).Append(Environment.NewLine);
        }

        _taking.Clear();

        return _batch.ToString();
    }
}
