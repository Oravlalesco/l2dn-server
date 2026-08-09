namespace L2Dn.GameServer.AI.Scheduling;

internal interface INpcThinkClock
{
    long GetTimestamp();
    TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp);
    long Add(long timestamp, TimeSpan duration);
}

internal sealed class StopwatchNpcThinkClock: INpcThinkClock
{
    public static StopwatchNpcThinkClock Instance { get; } = new();

    private StopwatchNpcThinkClock()
    {
    }

    public long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();

    public TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp) =>
        System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp, endTimestamp);

    public long Add(long timestamp, TimeSpan duration) => checked(timestamp +
        (long)(duration.TotalSeconds * System.Diagnostics.Stopwatch.Frequency));
}
