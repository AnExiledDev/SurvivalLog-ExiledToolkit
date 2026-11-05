using System.Diagnostics;

namespace ExiledToolkit.Kit;

/// <summary>
/// Seconds on a clock that only moves forward, read without a call into the game: the interop getters for Unity's
/// clocks (Time.unscaledTime, Time.realtimeSinceStartup) box their answer on every read. Any thread. Toolkit 0.19.0 and
/// newer.
/// </summary>
public static class Clock
{
    private static readonly double TicksPerSecond = Stopwatch.Frequency;

    public static double Now => Stopwatch.GetTimestamp() / TicksPerSecond;
}
