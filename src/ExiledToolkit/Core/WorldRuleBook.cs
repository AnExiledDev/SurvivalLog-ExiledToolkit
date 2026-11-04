using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ExiledToolkit.Core;

internal enum RuleKind
{
    Bool,
    Int,
    Float,
    Choice,

    /// <summary>A text setting that holds a number; stored like a float.</summary>
    NumberText,
}

/// <summary>
/// How a world rule's value is stored in a save: bool as 0 or 1, int as is, float or double times 1000, a choice as its index,
/// and a text setting without a list of choices as the number it holds, times 1000. Works on the serialized text BepInEx writes to the cfg, so it needs none of the config types.
/// </summary>
internal sealed class RuleCodec
{
    public const int FloatScale = 1000;

    private readonly string[] _choices;

    private RuleCodec(RuleKind kind, string[] choices)
    {
        Kind = kind;
        _choices = choices;
    }

    public RuleKind Kind { get; }

    public IReadOnlyList<string> Choices => _choices;

    /// <summary>
    /// The codec for a setting of this type, or null when the type cannot be a world rule (flags, anything else). Text
    /// with a list of choices is a choice; text without one holds a number. <paramref name="textChoices"/> is the list
    /// a text setting accepts, if any.
    /// </summary>
    public static RuleCodec? For(Type type, IReadOnlyList<string>? textChoices)
    {
        if (type == typeof(bool)) return new RuleCodec(RuleKind.Bool, Array.Empty<string>());
        if (type == typeof(int)) return new RuleCodec(RuleKind.Int, Array.Empty<string>());
        if (type == typeof(float) || type == typeof(double)) return new RuleCodec(RuleKind.Float, Array.Empty<string>());
        if (type.IsEnum && !type.IsDefined(typeof(FlagsAttribute), false)) return new RuleCodec(RuleKind.Choice, Enum.GetNames(type));
        if (type == typeof(string) && textChoices != null && textChoices.Count > 0) return new RuleCodec(RuleKind.Choice, textChoices.ToArray());
        if (type == typeof(string)) return new RuleCodec(RuleKind.NumberText, Array.Empty<string>());

        return null;
    }

    public bool TryEncode(string serialized, out int stored)
    {
        stored = 0;
        var text = (serialized ?? "").Trim();

        switch (Kind)
        {
            case RuleKind.Bool:
                if (!bool.TryParse(text, out var flag)) return false;
                stored = flag ? 1 : 0;
                return true;
            case RuleKind.Int:
                return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out stored);
            case RuleKind.Float:
            case RuleKind.NumberText:
                if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || float.IsNaN(number) || float.IsInfinity(number)) return false;
                var scaled = Math.Round((double)number * FloatScale);
                if (scaled > int.MaxValue || scaled < int.MinValue) return false;
                stored = (int)scaled;
                return true;
            default:
                // Enum names parse ignoring case in BepInEx; a text list is matched the same way here.
                var index = Array.FindIndex(_choices, c => string.Equals(c, text, StringComparison.OrdinalIgnoreCase));
                if (index < 0) return false;
                stored = index;
                return true;
        }
    }

    public bool TryDecode(int stored, out string serialized)
    {
        serialized = "";

        switch (Kind)
        {
            case RuleKind.Bool:
                if (stored != 0 && stored != 1) return false;
                serialized = stored == 1 ? "true" : "false";
                return true;
            case RuleKind.Int:
                serialized = stored.ToString(CultureInfo.InvariantCulture);
                return true;
            case RuleKind.Float:
            case RuleKind.NumberText:
                serialized = ((float)((double)stored / FloatScale)).ToString(CultureInfo.InvariantCulture);
                return true;
            default:
                if (stored < 0 || stored >= _choices.Length) return false;
                serialized = _choices[stored];
                return true;
        }
    }
}

/// <summary>One tagged setting, as the book sees it. The adapter wraps a BepInEx config entry.</summary>
internal interface IRuleSetting
{
    /// <summary>Stable across games: the owning plugin's GUID, the section and the key.</summary>
    string Id { get; }

    RuleCodec Codec { get; }

    /// <summary>The value in play, serialized.</summary>
    string Read();

    /// <summary>Sets the value in play. Fires the setting's change event, so the owning mod applies it live.</summary>
    void Write(string serialized);
}

/// <summary>
/// The world layer over global settings. While a world is loaded, a rule the world sets plays the world's value and
/// the book keeps the global value beside it, so the cfg can go on holding global. Everything else stays at global.
/// Safe from any thread; the change events it causes run on the calling thread.
/// </summary>
internal sealed class WorldRuleBook
{
    private readonly object _gate = new();
    private readonly Dictionary<IRuleSetting, Held> _held = new(ReferenceEqualityComparer.Instance);
    private int _writing;

    /// <summary>A world value was set (the stored number) or cleared (null) by an edit while the world plays.</summary>
    public event Action<string, int?>? WorldValueChanged;

    public bool InWorld { get; private set; }

    /// <summary>
    /// A world starts: first puts back anything the last world set, then plays each override the world stores. An
    /// override for a rule that is not installed, or that no longer decodes, is skipped and left in the save.
    /// </summary>
    public int Begin(IEnumerable<IRuleSetting> rules, IReadOnlyDictionary<string, int> overrides)
    {
        var applied = 0;

        lock (_gate)
        {
            RestoreAll();
            InWorld = true;

            foreach (var rule in rules)
            {
                if (!overrides.TryGetValue(rule.Id, out var stored) || !rule.Codec.TryDecode(stored, out var serialized)) continue;

                Play(rule, serialized, stored);
                applied++;
            }
        }

        return applied;
    }

    /// <summary>The world closes: every rule it set goes back to global.</summary>
    public void End()
    {
        lock (_gate)
        {
            RestoreAll();
            InWorld = false;
        }
    }

    public bool IsSetByWorld(IRuleSetting rule)
    {
        lock (_gate) return _held.ContainsKey(rule);
    }

    /// <summary>The global value, serialized: kept by the book while the world sets the rule, else the value in play.</summary>
    public string GlobalOf(IRuleSetting rule)
    {
        lock (_gate) return _held.TryGetValue(rule, out var held) ? held.Global : rule.Read();
    }

    /// <summary>For the cfg write: the global value of a rule the world sets, or null when the value in play is global.</summary>
    public string? HeldGlobal(IRuleSetting rule)
    {
        lock (_gate) return _held.TryGetValue(rule, out var held) ? held.Global : null;
    }

    /// <summary>Sets this world's value and plays it. False outside a world or when the value does not encode.</summary>
    public bool SetWorld(IRuleSetting rule, string serialized)
    {
        int stored;

        lock (_gate)
        {
            if (!InWorld || !rule.Codec.TryEncode(serialized, out stored) || !rule.Codec.TryDecode(stored, out var exact)) return false;

            Play(rule, exact, stored);
        }

        WorldValueChanged?.Invoke(rule.Id, stored);
        return true;
    }

    /// <summary>Drops this world's value, so the rule plays global again. False when the world did not set it.</summary>
    public bool ClearWorld(IRuleSetting rule)
    {
        lock (_gate)
        {
            if (!_held.TryGetValue(rule, out var held)) return false;

            _held.Remove(rule);
            WriteOwn(rule, held.Global);
        }

        WorldValueChanged?.Invoke(rule.Id, null);
        return true;
    }

    /// <summary>
    /// Sets the global value. While the world sets the rule only the kept value changes and <paramref name="save"/>
    /// writes the cfg; the value in play stays the world's. Otherwise the value in play is global and changes.
    /// </summary>
    public void SetGlobal(IRuleSetting rule, string serialized, Action save)
    {
        bool isHeld;

        lock (_gate)
        {
            isHeld = _held.TryGetValue(rule, out var held);

            if (isHeld) held!.Global = serialized;
            else rule.Write(serialized);
        }

        if (isHeld) save();
    }

    /// <summary>
    /// A rule's value changed. The book's own writes are ignored. A change to a rule the world sets, from anywhere
    /// else (the Mod Manager, the owning mod), is an edit of the world's value.
    /// </summary>
    public void Changed(IRuleSetting rule)
    {
        int stored;

        lock (_gate)
        {
            if (_writing > 0 || !_held.TryGetValue(rule, out var held)) return;
            if (!rule.Codec.TryEncode(rule.Read(), out stored) || stored == held.World) return;

            held.World = stored;
        }

        WorldValueChanged?.Invoke(rule.Id, stored);
    }

    // Under the lock.
    private void Play(IRuleSetting rule, string serialized, int stored)
    {
        if (_held.TryGetValue(rule, out var held)) held.World = stored;
        else _held[rule] = new Held(rule.Read(), stored);

        WriteOwn(rule, serialized);
    }

    // Under the lock.
    private void RestoreAll()
    {
        var held = _held.ToArray();
        _held.Clear();

        foreach (var pair in held) WriteOwn(pair.Key, pair.Value.Global);
    }

    // Under the lock, which is re-entrant: the change event this write fires on this thread finds _writing set. A
    // change from another thread waits for the lock and finds it clear again.
    private void WriteOwn(IRuleSetting rule, string serialized)
    {
        _writing++;

        try
        {
            rule.Write(serialized);
        }
        finally
        {
            _writing--;
        }
    }

    private sealed class Held
    {
        public Held(string global, int world)
        {
            Global = global;
            World = world;
        }

        public string Global { get; set; }

        public int World { get; set; }
    }
}
