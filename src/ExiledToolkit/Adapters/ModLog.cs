using System;
using BepInEx;
using BepInEx.Logging;
using ExiledToolkit.Game;

namespace ExiledToolkit.Kit;

/// <summary>
/// A mod's log: each line goes to the BepInEx log (a "could not hook" line as an error, so the Mod Manager can show the
/// mod may need an update) and to the mod's own file under the BepInEx folder, which survives restarts. The file is
/// written by a Toolkit worker thread, so logging never waits on the disk. Any thread. Toolkit 0.19.0 and newer.
/// </summary>
public sealed class ModLog
{
    private readonly ManualLogSource _bepinex;
    private readonly LogFile _file;

    /// <param name="bepinex">The plugin's <c>Log</c>.</param>
    /// <param name="fileName">The file under the BepInEx folder, such as "farm-hub.log"; a full path is used as it is.</param>
    public ModLog(ManualLogSource bepinex, string fileName)
    {
        _bepinex = bepinex ?? throw new ArgumentNullException(nameof(bepinex));
        _file = new LogFile(System.IO.Path.Combine(Paths.BepInExRootPath, fileName ?? throw new ArgumentNullException(nameof(fileName))));
    }

    public string Path => _file.Path;

    /// <summary>Whether lines also go to the file, say from a Diagnostics setting. Default: on.</summary>
    public bool IsFileOn
    {
        get => _file.IsOn;
        set => _file.IsOn = value;
    }

    /// <summary>The size the file is kept under, in bytes: past it, it is moved to "&lt;name&gt;.1" and started again. 0 (the default) for no limit.</summary>
    public long MaxFileBytes
    {
        get => _file.MaxBytes;
        set => _file.MaxBytes = value;
    }

    /// <summary>
    /// Whether each line is written to the file on the thread that logs it, before the call returns, instead of by the
    /// worker. Slower, but a crash cannot lose the last lines; for a mod that logs to find out why the game crashed.
    /// Default: off. Toolkit 0.20.0 and newer.
    /// </summary>
    public bool WritesAtOnce
    {
        get => _file.WritesAtOnce;
        set => _file.WritesAtOnce = value;
    }

    /// <summary>Never throws: a logger that can fail would take down whatever it was asked to report on.</summary>
    public void Write(string message) => Write(message, HookFailure.Is(message));

    /// <summary>The same, as an error in the BepInEx log whatever the line says, so the Mod Manager shows the mod red. Never throws. Toolkit 0.20.0 and newer.</summary>
    public void WriteError(string message) => Write(message, true);

    /// <summary>For lines too many for the BepInEx console: they go to the file alone (while <see cref="IsFileOn"/>). Never throws.</summary>
    public void WriteFileOnly(string message) => _file.Add(DateTime.UtcNow, message);

    /// <summary>
    /// Moves what the file holds to "&lt;name&gt;.1" so it starts empty, for a log that is only about the current game
    /// start. Call it once, before the first line. Never throws. Toolkit 0.20.0 and newer.
    /// </summary>
    public void StartFresh() => _file.StartFresh();

    private void Write(string message, bool isError)
    {
        try
        {
            if (isError) _bepinex.LogError(message);
            else _bepinex.LogInfo(message);
        }
        catch (Exception)
        {
            // The file copy below still has a chance.
        }

        _file.Add(DateTime.UtcNow, message);
    }

    /// <summary>Writes the waiting lines now, on this thread, say before a crash-prone step. The Toolkit also does it when the game closes.</summary>
    public void Flush() => _file.Flush();
}
