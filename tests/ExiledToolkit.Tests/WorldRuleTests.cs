using ExiledToolkit.Core;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>
/// What a player can count on from world rules: a world plays its own values, every other world and the cfg keep the
/// global ones, and an edit lands on the layer it was meant for.
/// </summary>
public class WorldRuleTests
{
    private readonly WorldRuleBook _book = new();
    private readonly List<(string Id, int? Value)> _stored = new();

    public WorldRuleTests() => _book.WorldValueChanged += (id, value) => _stored.Add((id, value));

    /// <summary>Like a BepInEx entry: a write fires the change event at once, on the writing thread.</summary>
    private sealed class FakeRule : IRuleSetting
    {
        private readonly WorldRuleBook _book;
        private string _value;

        public FakeRule(WorldRuleBook book, string id, Type type, string value, IReadOnlyList<string>? choices = null)
        {
            _book = book;
            Clamp = v => v;
            Id = id;
            Codec = RuleCodec.For(type, choices)!;
            _value = value;
        }

        public string Id { get; }

        public RuleCodec Codec { get; }

        public int Writes { get; private set; }

        /// <summary>Like an AcceptableValueRange: the entry keeps the nearest value it accepts.</summary>
        public Func<string, string> Clamp { get; set; }

        public string Read() => _value;

        public void Write(string serialized)
        {
            serialized = Clamp(serialized);
            if (_value == serialized) return;

            _value = serialized;
            Writes++;
            _book.Changed(this);
        }

        /// <summary>What the cfg file would get if it were written now.</summary>
        public string InFile() => _book.HeldGlobal(this) ?? _value;
    }

    private enum Weather { Mild, Normal, Harsh }

    [Flags]
    private enum Parts { None = 0, A = 1, B = 2 }

    private FakeRule Rule(string id, Type type, string value) => new(_book, id, type, value);

    [Fact]
    public void A_world_plays_its_own_values_and_leaves_the_rest_at_global()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        var waves = Rule("gr|Waves|Off", typeof(bool), "false");

        var applied = _book.Begin(new[] { loot, waves }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7 });

        Assert.Equal(1, applied);
        Assert.Equal("7", loot.Read());
        Assert.Equal("false", waves.Read());
        Assert.True(_book.IsSetByWorld(loot));
        Assert.False(_book.IsSetByWorld(waves));
    }

    [Fact]
    public void The_cfg_keeps_global_while_a_world_plays_its_own_value()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");

        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7 });

        Assert.Equal("7", loot.Read());
        Assert.Equal("3", loot.InFile());
        Assert.Equal("3", _book.GlobalOf(loot));
    }

    [Fact]
    public void Closing_the_world_puts_global_back()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7 });

        _book.End();

        Assert.Equal("3", loot.Read());
        Assert.False(_book.IsSetByWorld(loot));
        Assert.False(_book.InWorld);
        Assert.Empty(_stored);
    }

    [Fact]
    public void The_next_world_starts_from_global_even_when_the_last_one_never_closed()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        var waves = Rule("gr|Waves|Off", typeof(bool), "false");
        _book.Begin(new[] { loot, waves }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7, ["gr|Waves|Off"] = 1 });

        _book.Begin(new[] { loot, waves }, new Dictionary<string, int> { ["gr|Waves|Off"] = 1 });

        Assert.Equal("3", loot.Read());
        Assert.Equal("true", waves.Read());
        Assert.Equal("false", waves.InFile());
    }

    [Fact]
    public void Editing_a_rule_the_world_sets_changes_the_world_value_and_not_global()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7 });

        loot.Write("9");

        Assert.Equal(new[] { ("lr|Loot|Days", (int?)9) }, _stored);
        Assert.Equal("3", loot.InFile());
        _book.End();
        Assert.Equal("3", loot.Read());
    }

    [Fact]
    public void Editing_a_rule_the_world_does_not_set_is_a_global_edit()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        _book.Begin(new[] { loot }, new Dictionary<string, int>());

        loot.Write("5");
        _book.End();

        Assert.Empty(_stored);
        Assert.Equal("5", loot.Read());
    }

    [Fact]
    public void Setting_a_world_value_plays_it_and_tells_the_store()
    {
        var days = Rule("dl|Day|Minutes", typeof(float), "24");
        _book.Begin(new[] { days }, new Dictionary<string, int>());

        Assert.True(_book.SetWorld(days, "36.5"));

        Assert.Equal("36.5", days.Read());
        Assert.Equal("24", days.InFile());
        Assert.Equal(new[] { ("dl|Day|Minutes", (int?)36500) }, _stored);
    }

    [Fact]
    public void Setting_a_world_value_twice_still_keeps_the_first_global()
    {
        var days = Rule("dl|Day|Minutes", typeof(float), "24");
        _book.Begin(new[] { days }, new Dictionary<string, int>());

        _book.SetWorld(days, "30");
        _book.SetWorld(days, "36");
        _book.End();

        Assert.Equal("24", days.Read());
    }

    [Fact]
    public void A_world_value_cannot_be_set_outside_a_world()
    {
        var days = Rule("dl|Day|Minutes", typeof(float), "24");

        Assert.False(_book.SetWorld(days, "36"));
        Assert.Equal("24", days.Read());
        Assert.Empty(_stored);
    }

    [Fact]
    public void Clearing_a_world_value_plays_global_again()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7 });

        Assert.True(_book.ClearWorld(loot));

        Assert.Equal("3", loot.Read());
        Assert.False(_book.IsSetByWorld(loot));
        Assert.Equal(new[] { ("lr|Loot|Days", (int?)null) }, _stored);
        Assert.False(_book.ClearWorld(loot));
    }

    [Fact]
    public void Setting_global_while_the_world_sets_the_rule_changes_only_the_cfg()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7 });
        var saves = 0;

        _book.SetGlobal(loot, "4", () => saves++);

        Assert.Equal("7", loot.Read());
        Assert.Equal("4", loot.InFile());
        Assert.Equal(1, saves);
        Assert.Empty(_stored);
        _book.End();
        Assert.Equal("4", loot.Read());
    }

    [Fact]
    public void Setting_global_for_a_rule_the_world_leaves_alone_changes_the_value_in_play()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        _book.Begin(new[] { loot }, new Dictionary<string, int>());
        var saves = 0;

        _book.SetGlobal(loot, "4", () => saves++);

        Assert.Equal("4", loot.Read());
        Assert.Equal(0, saves);
    }

    [Fact]
    public void An_override_for_a_missing_mod_or_a_bad_number_is_skipped()
    {
        var waves = Rule("gr|Waves|Off", typeof(bool), "false");

        var applied = _book.Begin(new[] { waves }, new Dictionary<string, int> { ["gone|A|B"] = 1, ["gr|Waves|Off"] = 5 });

        Assert.Equal(0, applied);
        Assert.Equal("false", waves.Read());
        Assert.False(_book.IsSetByWorld(waves));
    }

    [Fact]
    public void The_layers_own_writes_are_not_taken_for_edits()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");

        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7 });
        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 8 });
        _book.End();

        Assert.Empty(_stored);
        Assert.Equal(4, loot.Writes);
    }

    [Fact]
    public void A_world_value_the_mod_clamps_is_not_rewritten_by_playing_it()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        loot.Clamp = v => int.Parse(v) > 10 ? "10" : v;

        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 50 });

        Assert.Equal("10", loot.Read());
        Assert.Empty(_stored);
    }

    [Fact]
    public void An_edit_from_another_thread_still_counts_as_a_world_edit()
    {
        var loot = Rule("lr|Loot|Days", typeof(int), "3");
        _book.Begin(new[] { loot }, new Dictionary<string, int> { ["lr|Loot|Days"] = 7 });

        Task.Run(() => loot.Write("9")).Wait();

        Assert.Equal(new[] { ("lr|Loot|Days", (int?)9) }, _stored);
    }

    [Theory]
    [InlineData(typeof(bool), "true", 1)]
    [InlineData(typeof(bool), "False", 0)]
    [InlineData(typeof(int), "-12", -12)]
    [InlineData(typeof(float), "1.5", 1500)]
    [InlineData(typeof(float), "0.0004", 0)]
    [InlineData(typeof(float), "-0.25", -250)]
    public void Values_are_stored_as_whole_numbers(Type type, string serialized, int stored)
    {
        var codec = RuleCodec.For(type, null)!;

        Assert.True(codec.TryEncode(serialized, out var actual));
        Assert.Equal(stored, actual);
    }

    [Fact]
    public void A_stored_float_reads_back_as_the_cfg_writes_it()
    {
        var codec = RuleCodec.For(typeof(float), null)!;

        Assert.True(codec.TryDecode(1500, out var serialized));
        Assert.Equal("1.5", serialized);
        Assert.True(codec.TryDecode(-250, out serialized));
        Assert.Equal("-0.25", serialized);
    }

    [Fact]
    public void A_choice_is_stored_as_its_place_in_the_list()
    {
        var weather = RuleCodec.For(typeof(Weather), null)!;
        var mode = RuleCodec.For(typeof(string), new[] { "Off", "Some", "All" })!;

        Assert.True(weather.TryEncode("harsh", out var stored));
        Assert.Equal(2, stored);
        Assert.True(weather.TryDecode(1, out var name));
        Assert.Equal("Normal", name);
        Assert.True(mode.TryEncode("All", out stored));
        Assert.Equal(2, stored);
        Assert.False(mode.TryDecode(3, out _));
        Assert.False(mode.TryEncode("Most", out _));
    }

    [Fact]
    public void Free_text_flags_and_other_types_cannot_be_world_rules()
    {
        Assert.Null(RuleCodec.For(typeof(string), null));
        Assert.Null(RuleCodec.For(typeof(string), Array.Empty<string>()));
        Assert.Null(RuleCodec.For(typeof(Parts), null));
        Assert.Null(RuleCodec.For(typeof(double), null));
    }

    [Fact]
    public void A_bool_rule_refuses_numbers_other_than_0_and_1()
    {
        var codec = RuleCodec.For(typeof(bool), null)!;

        Assert.False(codec.TryDecode(2, out _));
        Assert.False(codec.TryEncode("yes", out _));
    }
}
