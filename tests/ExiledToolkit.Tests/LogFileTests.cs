using ExiledToolkit.Kit;
using Xunit;

namespace ExiledToolkit.Tests;

/// <summary>A mod's own log file: every line kept in order across restarts, nothing kept while it is off, and a size cap that moves the old file aside.</summary>
public sealed class LogFileTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "etk-logfile-" + Guid.NewGuid().ToString("N"));

    public LogFileTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, true);
        }
        catch (IOException)
        {
            // The worker may still hold the file a moment; the temp folder is cleaned up by the system.
        }
    }

    [Fact]
    public void Lines_are_appended_in_order_after_what_the_file_already_held()
    {
        var path = Path.Combine(_folder, "mod.log");

        File.WriteAllText(path, "from last session" + Environment.NewLine);

        var file = new LogFile(path);

        file.Add(DateTime.UtcNow, "first");
        file.Add(DateTime.UtcNow, "second");
        file.Flush();

        var lines = File.ReadAllLines(path);

        Assert.Equal("from last session", lines[0]);
        Assert.EndsWith(" first", lines[1]);
        Assert.EndsWith(" second", lines[2]);
        Assert.Equal(3, lines.Length);
    }

    [Fact]
    public void A_file_that_is_off_keeps_nothing()
    {
        var path = Path.Combine(_folder, "off.log");
        var file = new LogFile(path) { IsOn = false };

        file.Add(DateTime.UtcNow, "dropped");
        file.Flush();
        file.IsOn = true;
        file.Add(DateTime.UtcNow, "kept");
        file.Flush();

        Assert.Single(File.ReadAllLines(path), line => line.EndsWith(" kept", StringComparison.Ordinal));
        Assert.DoesNotContain("dropped", File.ReadAllText(path));
    }

    [Fact]
    public void A_full_file_is_moved_aside_and_started_again()
    {
        var path = Path.Combine(_folder, "capped.log");
        var file = new LogFile(path) { MaxBytes = 200 };

        File.WriteAllText(path, new string('x', 190) + Environment.NewLine);

        file.Add(DateTime.UtcNow, "after the cap");
        file.Flush();

        Assert.StartsWith(new string('x', 190), File.ReadAllText(path + ".1"));
        Assert.EndsWith(" after the cap", File.ReadAllLines(path).Single());
    }

    [Fact]
    public void A_file_that_writes_at_once_holds_each_line_before_Add_returns()
    {
        // A crash right after a line is logged must not lose it, so nothing may wait on the worker.
        var path = Path.Combine(_folder, "atonce.log");
        var file = new LogFile(path) { WritesAtOnce = true };

        file.Add(DateTime.UtcNow, "before the crash");

        Assert.EndsWith(" before the crash", File.ReadAllLines(path).Single());
    }

    [Fact]
    public void Starting_fresh_keeps_the_last_run_in_dot_1_and_empties_the_file()
    {
        var path = Path.Combine(_folder, "fresh.log");

        File.WriteAllText(path, "last game start" + Environment.NewLine);
        File.WriteAllText(path + ".1", "two starts ago" + Environment.NewLine);

        var file = new LogFile(path);

        file.StartFresh();
        file.Add(DateTime.UtcNow, "this game start");
        file.Flush();

        Assert.Equal("last game start", File.ReadAllLines(path + ".1").Single());
        Assert.EndsWith(" this game start", File.ReadAllLines(path).Single());
    }

    [Fact]
    public void Starting_fresh_with_no_file_yet_changes_nothing()
    {
        var path = Path.Combine(_folder, "new.log");
        var file = new LogFile(path);

        file.StartFresh();
        file.Add(DateTime.UtcNow, "first");
        file.Flush();

        Assert.False(File.Exists(path + ".1"));
        Assert.EndsWith(" first", File.ReadAllLines(path).Single());
    }

    [Fact]
    public void Lines_added_from_many_threads_all_reach_the_file()
    {
        var path = Path.Combine(_folder, "busy.log");
        var file = new LogFile(path);

        Parallel.For(0, 200, i => file.Add(DateTime.UtcNow, "line " + i));
        file.Flush();

        Assert.Equal(200, File.ReadAllLines(path).Length);
    }
}
