using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace ExiledToolkit.Web;

/// <summary>
/// The icon files the game ships for its web UI, indexed once for every mod, so their pages can show the same pictures.
/// The folder holds about 1800 files, so it is walked on a worker; only the folder's path is asked of Unity, on the
/// main thread. Toolkit 0.19.0 and newer.
/// </summary>
public static class WebIcons
{
    private static readonly object Gate = new();
    private static Task<(IconIndex Icons, string Note)>? _scan;

    /// <summary>The index once the walk is done; <see cref="IconIndex.Empty"/> until then or when the folder could not be read.</summary>
    public static IconIndex Icons => _scan is { IsCompletedSuccessfully: true } scan ? scan.Result.Icons : IconIndex.Empty;

    public static bool IsReady => _scan is { IsCompleted: true };

    /// <summary>
    /// Starts the walk on the first call, which must be on the main thread; every later call gets the same task. The
    /// task never fails: a folder that cannot be read gives the empty index, and the note says why (the worker does not
    /// log, so log the note from the main thread when it is done).
    /// </summary>
    public static Task<(IconIndex Icons, string Note)> StartScan()
    {
        lock (Gate)
        {
            if (_scan != null) return _scan;

            try
            {
                var webUi = Path.Combine(Application.streamingAssetsPath, "WebUI");

                return _scan = Task.Run(() => Scan(webUi));
            }
            catch (Exception ex)
            {
                return _scan = Task.FromResult((IconIndex.Empty, "no web icons: " + ex.Message));
            }
        }
    }

    private static (IconIndex, string) Scan(string webUi)
    {
        var res = Path.Combine(webUi, "Res");

        try
        {
            var files = new List<string>();

            foreach (var file in Directory.EnumerateFiles(res, "*.png", SearchOption.AllDirectories)) files.Add(Path.GetRelativePath(webUi, file));

            return (IconIndex.Build(files), $"{files.Count} web icons found under {res}");
        }
        catch (Exception ex)
        {
            return (IconIndex.Empty, $"no web icons under {res}: {ex.Message}");
        }
    }
}
