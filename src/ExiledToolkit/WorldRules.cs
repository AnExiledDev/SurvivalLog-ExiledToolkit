using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using ExiledToolkit.Adapters;
using ExiledToolkit.Core;
using HarmonyLib;

namespace ExiledToolkit;

/// <summary>
/// Settings a world can set for itself. A mod opts a setting in with the plain string tag <see cref="Tag"/> in its
/// ConfigDescription tags, and only settings it applies live through <see cref="LiveSettings.Watch(Action, ConfigEntryBase[])"/>
/// count. While a world plays, a rule it sets has the world's value and the cfg keeps the global one; every other
/// rule stays at global. The world's values are kept by whichever mod calls <see cref="Begin"/> (Customizable
/// Difficulty keeps them in the save); this class only plays them.
/// </summary>
public static class WorldRules
{
    /// <summary>The ConfigDescription tag that makes a live setting a world rule.</summary>
    public const string Tag = "ModManager.WorldRule";

    internal static readonly WorldRuleBook Book = new();

    private static readonly object Gate = new();
    private static readonly Dictionary<ConfigEntryBase, EntryRule> Found = new(ReferenceEqualityComparer.Instance);
    private static readonly HashSet<ConfigEntryBase> Refused = new(ReferenceEqualityComparer.Instance);
    private static readonly HashSet<ConfigFile> Listening = new();
    private static Action<string> _log = _ => { };
    private static volatile bool _isScanned;

    /// <summary>
    /// A world value was set (the stored number) or cleared (null) while the world plays, by the Mod Manager, by the
    /// owning mod or through <see cref="SetWorld"/>. The keeper of the world's values stores it. Runs on the thread
    /// that made the change.
    /// </summary>
    public static event Action<string, int?> WorldValueChanged
    {
        add => Book.WorldValueChanged += value;
        remove => Book.WorldValueChanged -= value;
    }

    /// <summary>Whether a world is playing, between <see cref="Begin"/> and <see cref="End"/>.</summary>
    public static bool InWorld => Book.InWorld;

    /// <summary>Every world rule of every loaded mod, in plugin load order.</summary>
    public static IReadOnlyList<ConfigEntryBase> All()
    {
        Scan(force: true);

        lock (Gate) return Found.Keys.ToList();
    }

    public static bool IsRule(ConfigEntryBase entry) => RuleOfScanned(entry) != null;

    /// <summary>The id a world stores a rule under: the plugin's GUID, the section and the key, joined by "|".</summary>
    public static string? IdOf(ConfigEntryBase entry) => RuleOfScanned(entry)?.Id;

    /// <summary>
    /// A world starts. Puts back whatever the last world set, then plays each of this world's values, keyed by
    /// <see cref="IdOf"/>. Values for mods that are not installed are skipped. Main thread. Returns how many were played.
    /// </summary>
    public static int Begin(IReadOnlyDictionary<string, int> worldValues)
    {
        if (worldValues == null) throw new ArgumentNullException(nameof(worldValues));

        Scan(force: true);

        EntryRule[] rules;
        lock (Gate) rules = Found.Values.ToArray();

        var played = Book.Begin(rules, worldValues);
        _log("world rules: a world started, " + played + " of " + rules.Length + " rules set by it");

        return played;
    }

    /// <summary>The world closes: every rule it set plays global again. Main thread.</summary>
    public static void End()
    {
        Book.End();
        _log("world rules: the world closed, every rule is back at global");
    }

    public static bool IsSetByWorld(ConfigEntryBase entry) => RuleOfScanned(entry) is { } rule && Book.IsSetByWorld(rule);

    /// <summary>The global value, serialized as in the cfg: the one the cfg holds, even while the world sets another.</summary>
    public static string? GlobalValue(ConfigEntryBase entry) => RuleOfScanned(entry) is { } rule ? Book.GlobalOf(rule) : null;

    /// <summary>
    /// Sets this world's value (serialized as in the cfg) and plays it; the cfg keeps global. False outside a world,
    /// for a setting that is not a rule, or for a value the rule cannot hold.
    /// </summary>
    public static bool SetWorld(ConfigEntryBase entry, string serialized) => RuleOfScanned(entry) is { } rule && Book.SetWorld(rule, serialized);

    /// <summary>Drops this world's value, so the rule plays global again. False when the world did not set it.</summary>
    public static bool ClearWorld(ConfigEntryBase entry) => RuleOfScanned(entry) is { } rule && Book.ClearWorld(rule);

    /// <summary>
    /// Sets the global value (serialized as in the cfg) and writes the cfg. While the world sets the rule, the value
    /// in play stays the world's. False for a setting that is not a rule.
    /// </summary>
    public static bool SetGlobal(ConfigEntryBase entry, string serialized)
    {
        if (RuleOfScanned(entry) is not { } rule) return false;

        Book.SetGlobal(rule, serialized, () => entry.ConfigFile.Save());
        return true;
    }

    /// <summary>The number a world stores for this value, as <see cref="Begin"/> takes it.</summary>
    public static bool TryEncode(ConfigEntryBase entry, string serialized, out int stored)
    {
        stored = 0;

        return RuleOfScanned(entry) is { } rule && rule.Codec.TryEncode(serialized, out stored);
    }

    /// <summary>The value, serialized as in the cfg, of a number a world stores.</summary>
    public static bool TryDecode(ConfigEntryBase entry, int stored, out string serialized)
    {
        serialized = "";

        return RuleOfScanned(entry) is { } rule && rule.Codec.TryDecode(stored, out serialized);
    }

    internal static void Apply(Harmony harmony, Action<string> log)
    {
        _log = log;

        try
        {
            var save = AccessTools.Method(typeof(ConfigFile), nameof(ConfigFile.Save));
            harmony.Patch(save,
                prefix: new HarmonyMethod(typeof(WorldRules), nameof(SavePrefix)),
                finalizer: new HarmonyMethod(typeof(WorldRules), nameof(SaveFinalizer)));
            log("world rules: the cfg keeps global values while a world sets its own");
        }
        catch (Exception ex)
        {
            log("world rules: hooking ConfigFile.Save failed, so world rules are off: " + ex);
            IsOff = true;
        }
    }

    /// <summary>Without the save hook a world's value would end up in the cfg, so no rule is found and none plays.</summary>
    private static bool IsOff { get; set; }

    private static EntryRule? RuleOfScanned(ConfigEntryBase entry)
    {
        if (entry == null) return null;

        Scan(force: false);

        return RuleOf(entry);
    }

    private static EntryRule? RuleOf(ConfigEntryBase entry)
    {
        lock (Gate) return Found.TryGetValue(entry, out var rule) ? rule : null;
    }

    // Plugins bind settings and start watching them in Load. A forced scan (at each world start, and for the full list)
    // picks up whatever has loaded since the last one; the per-setting calls scan only once, so the Mod Manager can ask
    // about every setting it draws without walking every cfg each time.
    private static void Scan(bool force)
    {
        if (IsOff || (!force && _isScanned)) return;

        _isScanned = true;

        foreach (var plugin in IL2CPPChainloader.Instance.Plugins.Values.ToArray())
        {
            if (plugin.Instance is not BasePlugin { Config: { } config }) continue;

            foreach (var entry in config.Values.ToArray())
            {
                if (!entry.Description.Tags.Any(tag => tag is string text && text == Tag)) continue;

                lock (Gate)
                {
                    if (Found.ContainsKey(entry) || Refused.Contains(entry)) continue;
                }

                AddRule(plugin.Metadata.GUID, config, entry);
            }
        }
    }

    private static void AddRule(string guid, ConfigFile config, ConfigEntryBase entry)
    {
        var name = guid + " [" + entry.Definition.Section + "] " + entry.Definition.Key;

        if (!LiveSettings.IsWatched(entry)) return; // The mod may start watching later; the next scan looks again.

        var choices = entry.Description.AcceptableValues is AcceptableValueList<string> list ? list.AcceptableValues : null;
        var codec = RuleCodec.For(entry.SettingType, choices);
        var field = entry.GetType().GetField("_typedValue", BindingFlags.Instance | BindingFlags.NonPublic);

        lock (Gate)
        {
            if (Found.ContainsKey(entry) || Refused.Contains(entry)) return;

            if (codec == null || field == null)
            {
                // Remembered, so the log says it once.
                Refused.Add(entry);
                _log("world rules: " + name + " is tagged but a " + entry.SettingType.Name + " setting cannot be a world rule");
                return;
            }

            Found[entry] = new EntryRule(guid + "|" + entry.Definition.Section + "|" + entry.Definition.Key, entry, codec, field);

            if (Listening.Add(config)) config.SettingChanged += (_, args) => OnChanged(args.ChangedSetting);
        }
    }

    private static void OnChanged(ConfigEntryBase entry)
    {
        if (RuleOf(entry) is { } rule) Book.Changed(rule);
    }

    private sealed class Swap
    {
        public Swap(EntryRule rule, object? inPlay)
        {
            Rule = rule;
            InPlay = inPlay;
        }

        public EntryRule Rule { get; }

        public object? InPlay { get; }
    }

    // The cfg is written from each entry's value; for the time of the write a rule the world sets holds its global
    // value, so the file never holds a world's value and a crash mid-world leaves nothing to clean up. The field is set
    // directly, without the change event, and the write holds the file's lock.
    private static void SavePrefix(ConfigFile __instance, out Swap[]? __state)
    {
        __state = null;
        var swaps = new List<Swap>();

        try
        {
            EntryRule[] rules;
            lock (Gate) rules = Found.Values.Where(rule => ReferenceEquals(rule.Entry.ConfigFile, __instance)).ToArray();

            foreach (var rule in rules)
            {
                if (Book.HeldGlobal(rule) is not { } global) continue;

                var inPlay = rule.Field.GetValue(rule.Entry);
                rule.Field.SetValue(rule.Entry, TomlTypeConverter.ConvertToValue(global, rule.Entry.SettingType));
                swaps.Add(new Swap(rule, inPlay));
            }
        }
        catch (Exception ex)
        {
            _log("world rules: keeping global values in " + __instance.ConfigFilePath + " failed: " + ex);
        }

        // Whatever was swapped before a failure still goes back.
        __state = swaps.Count > 0 ? swaps.ToArray() : null;
    }

    private static Exception? SaveFinalizer(Exception? __exception, Swap[]? __state)
    {
        if (__state == null) return __exception;

        foreach (var swap in __state)
        {
            try
            {
                swap.Rule.Field.SetValue(swap.Rule.Entry, swap.InPlay);
            }
            catch (Exception ex)
            {
                _log("world rules: putting " + swap.Rule.Id + " back after a cfg write failed: " + ex);
            }
        }

        return __exception;
    }

    private sealed class EntryRule : IRuleSetting
    {
        public EntryRule(string id, ConfigEntryBase entry, RuleCodec codec, FieldInfo field)
        {
            Id = id;
            Entry = entry;
            Codec = codec;
            Field = field;
        }

        public string Id { get; }

        public ConfigEntryBase Entry { get; }

        public RuleCodec Codec { get; }

        public FieldInfo Field { get; }

        public string Read() => Entry.GetSerializedValue();

        public void Write(string serialized)
        {
            try
            {
                Entry.SetSerializedValue(serialized);
            }
            catch (Exception ex)
            {
                _log("world rules: setting " + Id + " to " + serialized + " failed: " + ex);
            }
        }
    }
}
