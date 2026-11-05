using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace ExiledToolkit.Kit;

/// <summary>
/// The file half of a mod's log: an append-only file that survives restarts (LogOutput.log is overwritten on the next
/// launch). Adding a line only queues it; one Toolkit worker thread writes every mod's waiting lines, so a frame that
/// logs never waits on the disk, and the worker never writes to the BepInEx console. Past <see cref="MaxBytes"/> the
/// file is moved to "&lt;name&gt;.1" and started again. When the game closes, waiting lines are written, giving up after
/// a short wait for a write in progress.
/// </summary>
internal sealed class LogFile
{
    private readonly object _writeGate = new();
    private readonly LogLines _lines = new();
    private long _size = -1;

    public LogFile(string path)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        LogWriter.Register(this);
    }

    public string Path { get; }

    /// <summary>Whether lines are kept for the file at all. Off drops them as they come.</summary>
    public volatile bool IsOn = true;

    /// <summary>The size the file is kept under, in bytes; 0 or less for no limit.</summary>
    public long MaxBytes { get; set; }

    /// <summary>Any thread. Never throws.</summary>
    public void Add(DateTime atUtc, string message)
    {
        if (!IsOn) return;

        try
        {
            if (_lines.Add(atUtc, message)) LogWriter.Wake(this);
        }
        catch (Exception)
        {
            // A line lost from the file copy costs nothing else.
        }
    }

    /// <summary>Writes every waiting line now, on the calling thread. Never throws.</summary>
    public void Flush()
    {
        lock (_writeGate) WriteWaiting();
    }

    /// <summary>The same, giving up when a write in progress takes longer than <paramref name="waitMilliseconds"/>.</summary>
    public void Flush(int waitMilliseconds)
    {
        if (!Monitor.TryEnter(_writeGate, waitMilliseconds)) return;

        try
        {
            WriteWaiting();
        }
        finally
        {
            Monitor.Exit(_writeGate);
        }
    }

    /// <summary>Under the write lock. The file is opened shared for reading, so the log can be tailed while the game runs.</summary>
    private void WriteWaiting()
    {
        try
        {
            var text = _lines.Take();

            if (text == null) return;

            var bytes = Encoding.UTF8.GetBytes(text);

            MakeRoom(bytes.Length);

            using (var file = new FileStream(Path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite)) file.Write(bytes, 0, bytes.Length);

            _size += bytes.Length;
        }
        catch (Exception)
        {
            // A locked or read-only file costs the file copy, never the mod; the size is read again next time.
            _size = -1;
        }
    }

    /// <summary>A file that a move cannot shift (another program holds it) keeps growing rather than lose lines.</summary>
    private void MakeRoom(int adding)
    {
        if (_size < 0) _size = File.Exists(Path) ? new FileInfo(Path).Length : 0;

        if (MaxBytes <= 0 || _size == 0 || _size + adding <= MaxBytes) return;

        try
        {
            File.Move(Path, Path + ".1", true);
            _size = 0;
        }
        catch (Exception)
        {
            // Written to the full file instead.
        }
    }
}

/// <summary>The one worker thread that writes every <see cref="LogFile"/>.</summary>
internal static class LogWriter
{
    private const int QuitWaitMilliseconds = 2000;

    private static readonly object Gate = new();
    private static readonly List<LogFile> Files = new();
    private static readonly ConcurrentQueue<LogFile> Due = new();
    private static readonly AutoResetEvent WakeUp = new(false);
    private static bool _hasStarted;

    public static void Register(LogFile file)
    {
        lock (Gate)
        {
            Files.Add(file);
            StartOnce();
        }
    }

    public static void Wake(LogFile file)
    {
        Due.Enqueue(file);
        WakeUp.Set();
    }

    /// <summary>Without the worker the lines wait in memory until the game closes; the BepInEx log still has every one.</summary>
    private static void StartOnce()
    {
        if (_hasStarted) return;

        _hasStarted = true;

        try
        {
            new Thread(WriteLoop) { IsBackground = true, Name = "Exiled's Toolkit mod logs" }.Start();
            AppDomain.CurrentDomain.ProcessExit += WriteOnQuit;
        }
        catch (Exception)
        {
            // Nothing else to do: see above.
        }
    }

    private static void WriteLoop()
    {
        try
        {
            while (true)
            {
                WakeUp.WaitOne();

                while (Due.TryDequeue(out var file)) file.Flush();
            }
        }
        catch (Exception)
        {
            // The file copies stop; the BepInEx log still has every line.
        }
    }

    private static void WriteOnQuit(object? sender, EventArgs e) => WriteAll();

    /// <summary>
    /// Writes every waiting line now. Called when the game quits (Unity's quit message, and ProcessExit, which an IL2CPP
    /// game may never raise); a second call finds nothing waiting.
    /// </summary>
    public static void WriteAll()
    {
        LogFile[] files;

        lock (Gate) files = Files.ToArray();

        foreach (var file in files) file.Flush(QuitWaitMilliseconds);
    }
}
