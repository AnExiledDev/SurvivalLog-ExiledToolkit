using System.Globalization;
using ExiledToolkit.Kit;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>The small helpers every mod used to carry its own copy of.</summary>
public class KitTests
{
    [Fact]
    public void A_phrase_shows_in_the_games_language_with_numbers_written_the_same_everywhere()
    {
        var phrase = new Phrase("Moved {0} items", "移动了 {0} 件物品");
        var culture = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            Assert.Equal("Moved 1.5 items", phrase.Say(false, 1.5));
            Assert.Equal("移动了 2 件物品", phrase.Say(true, 2));
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void A_line_the_game_wrote_reads_the_same_in_both_languages()
    {
        Assert.Equal("已存档", Phrase.FromGame("已存档").Pick(false));
    }

    [Fact]
    public void A_count_picks_the_singular_only_for_one_and_passes_the_rest_after_the_count()
    {
        var traps = new Counted(new Phrase("{0} trap in {1}", "{1}里有 {0} 个陷阱"), new Phrase("{0} traps in {1}", "{1}里有 {0} 个陷阱"));

        Assert.Equal("1 trap in the yard", traps.Say(false, 1, "the yard"));
        Assert.Equal("3 traps in the yard", traps.Say(false, 3, "the yard"));
        Assert.Equal("院子里有 3 个陷阱", traps.Say(true, 3, "院子"));
    }

    [Fact]
    public void A_sliced_job_runs_steps_until_the_slice_is_spent_and_always_moves_on()
    {
        var ran = new List<int>();
        var job = new SlicedJob(Steps(ran, 5));

        Assert.False(job.Advance(() => true));
        Assert.Equal(new[] { 0 }, ran);

        Assert.True(job.Advance(() => false));
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, ran);
        Assert.True(job.IsDone);
    }

    [Fact]
    public void A_step_that_waits_ends_the_slice_even_with_time_left()
    {
        var job = new SlicedJob(WaitThenFinish());

        Assert.False(job.Advance(() => false));
        Assert.True(job.Advance(() => false));
    }

    [Fact]
    public void An_unbound_key_never_asks_for_the_press_and_typing_is_asked_only_on_a_press()
    {
        var asked = new List<string>();

        Assert.False(Hotkey.Fires(false, () => { asked.Add("press"); return true; }, () => { asked.Add("typing"); return false; }));
        Assert.Empty(asked);

        Assert.False(Hotkey.Fires(true, () => false, () => { asked.Add("typing"); return false; }));
        Assert.Empty(asked);

        Assert.True(Hotkey.Fires(true, () => true, () => false));
        Assert.False(Hotkey.Fires(true, () => true, () => true));
        Assert.Equal("None", Hotkey.DefaultKey);
    }

    [Fact]
    public void A_key_of_any_type_fires_only_when_set_down_and_not_typing()
    {
        var asked = 0;

        Assert.False(KeyGate.Fires(0, 0, _ => { asked++; return true; }, () => false));
        Assert.Equal(0, asked);

        Assert.True(KeyGate.Fires(65, 0, key => key == 65, () => false));
        Assert.False(KeyGate.Fires(65, 0, key => key == 65, () => true));
        Assert.True(KeyGate.Fires(true, true, () => false));
        Assert.False(KeyGate.Fires(false, true, () => false));
    }

    [Fact]
    public void An_answer_to_an_older_call_is_not_taken_for_the_newer_ones()
    {
        var slot = new ReplySlot();
        var first = slot.Open();
        var second = slot.Open();

        slot.Offer(first, "late");
        Assert.Null(slot.Take());

        slot.Offer(second, "on time");
        Assert.Equal("on time", slot.Take());
        Assert.Null(slot.Take());
    }

    [Fact]
    public void Opening_a_call_drops_what_the_last_one_left_behind()
    {
        var slot = new ReplySlot();

        slot.Offer(slot.Open(), "old");
        slot.Open();

        Assert.Null(slot.Take());
    }

    [Fact]
    public void Log_lines_come_out_oldest_first_stamped_in_local_time()
    {
        var lines = new LogLines();
        var at = new DateTime(2026, 11, 5, 9, 30, 15, 250, DateTimeKind.Utc);

        Assert.True(lines.Add(at, "one"));
        Assert.False(lines.Add(at.AddSeconds(1), "two"));

        var text = lines.Take();

        Assert.Equal(
            at.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " one" + Environment.NewLine +
            at.AddSeconds(1).ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture) + " two" + Environment.NewLine,
            text);
        Assert.Null(lines.Take());
        Assert.True(lines.Add(at, "three"));
    }

    [Fact]
    public void The_clock_only_moves_forward()
    {
        var before = Clock.Now;

        Thread.Sleep(5);

        Assert.True(Clock.Now > before);
    }

    private static IEnumerable<bool> Steps(List<int> ran, int count)
    {
        for (var i = 0; i < count; i++)
        {
            ran.Add(i);

            yield return true;
        }
    }

    private static IEnumerable<bool> WaitThenFinish()
    {
        yield return false;
    }
}
