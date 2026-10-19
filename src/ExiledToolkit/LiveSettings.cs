using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using ExiledToolkit.Adapters;
using ExiledToolkit.Core;

namespace ExiledToolkit;

/// <summary>
/// Applies a mod's settings while the game runs, so a change made in the Mod Manager (or by anything else that sets
/// the value) takes effect without a restart. Watching a setting also tells the Mod Manager it applies live.
/// </summary>
public static class LiveSettings
{
    internal static readonly LiveQueue Queue = new();

    private static readonly HashSet<ConfigFile> Files = new();

    /// <summary>
    /// Calls <paramref name="apply"/> with the entry's value now, on the calling thread, and again on the game's main
    /// thread at the next frame after each change. Several changes in one frame make one call. An apply that throws
    /// is logged to the Toolkit's log and does not stop other mods. Dispose the result to stop watching.
    /// </summary>
    public static IDisposable Watch<T>(ConfigEntry<T> entry, Action<T> apply)
    {
        if (entry == null) throw new ArgumentNullException(nameof(entry));
        if (apply == null) throw new ArgumentNullException(nameof(apply));

        return Watch(() => apply(entry.Value), entry);
    }

    /// <summary>
    /// Calls <paramref name="apply"/> now, on the calling thread, and again on the game's main thread at the next frame
    /// after any of the entries changes. However many of them change in one frame, it is called once, so read them all
    /// inside it. An apply that throws is logged to the Toolkit's log and does not stop other mods. Dispose the result
    /// to stop watching.
    /// </summary>
    public static IDisposable Watch(Action apply, params ConfigEntryBase[] entries)
    {
        if (apply == null) throw new ArgumentNullException(nameof(apply));
        if (entries == null || entries.Length == 0 || entries.Any(e => e == null)) throw new ArgumentException("Watch needs at least one config entry, and no null ones.", nameof(entries));

        foreach (var file in entries.Select(e => e.ConfigFile).Distinct()) Listen(file);

        var watcher = Queue.Watch(entries, apply, NameOf(entries));

        watcher.Apply(ToastTicker.Log);

        return watcher;
    }

    /// <summary>For the Mod Manager: whether some mod applies this setting live through <see cref="Watch(Action, ConfigEntryBase[])"/>.</summary>
    internal static bool IsWatched(ConfigEntryBase entry) => Queue.IsWatched(entry);

    /// <summary>Main thread only, once a frame.</summary>
    internal static void Run(Action<string> log) => Queue.Run(log);

    // One listener per config file, kept for the life of the game; a change to an unwatched setting finds no watcher.
    private static void Listen(ConfigFile file)
    {
        lock (Files)
        {
            if (!Files.Add(file)) return;
        }

        file.SettingChanged += (_, args) => Queue.Changed(args.ChangedSetting);
    }

    private static string NameOf(ConfigEntryBase[] entries) =>
        System.IO.Path.GetFileName(entries[0].ConfigFile.ConfigFilePath) + " " + string.Join(", ", entries.Select(e => "[" + e.Definition.Section + "] " + e.Definition.Key));
}
