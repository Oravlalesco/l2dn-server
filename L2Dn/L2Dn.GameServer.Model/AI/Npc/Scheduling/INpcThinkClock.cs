namespace L2Dn.GameServer.AI.Scheduling;

internal interface INpcThinkClock
{
    long GetTimestamp();
    TimeSpan GetElapsedTime(long startTimestamp, long endTimestamp);
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
}

