using ExiledToolkit.Core;

namespace ExiledToolkit.Tests;

/// <summary>A toast center on a hand-moved clock, with a listener that keeps every record and a log that keeps every line.</summary>
internal sealed class Harness
{
    public static readonly DateTime Start = new(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc);

    public Harness(int maxVisible = 4, bool isEnabled = true, double seconds = 7)
    {
        Center = new ToastCenter(
            new ToastSettings(ToastPosition.TopRight, seconds, maxVisible, isEnabled),
            new ToastClock(() => Now, () => Utc),
            () => IsMainThread,
            Logs.Add);
    }

    public ToastCenter Center { get; }

    public long Now { get; set; }

    public DateTime Utc { get; set; } = Start;

    public bool IsMainThread { get; set; } = true;

    public List<string> Logs { get; } = new();

    public List<ToastRecord> Heard { get; } = new();

    public ToastId Show(ToastRequest request) => Center.Show(request);

    public IReadOnlyList<PageOp> Pump() => Center.Pump(new Action<ToastRecord>[] { Heard.Add });

    /// <summary>The page reports a box gone, showing the count it last drew; by default its time ran out.</summary>
    public void Gone(ToastId id, int shownCount = 1, bool isClosedByPlayer = false) => Center.Gone(id, shownCount, isClosedByPlayer);

    public static ToastRequest Stamina() => new("Survival Log", "Not enough stamina") { Body = "Rest first", Kind = ToastKind.Warning, Icon = "icons/stamina.png" };

    public static ToastRequest Titled(string title) => new("Storage Hub", title);
}
