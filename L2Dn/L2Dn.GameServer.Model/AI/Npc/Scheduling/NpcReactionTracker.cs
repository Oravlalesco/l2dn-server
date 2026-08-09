using L2Dn.GameServer.AI.Runtime;

namespace L2Dn.GameServer.AI.Scheduling;

internal static class NpcReactionTracker
{
    private static readonly AsyncLocal<Execution?> CurrentExecution = new();

    public static IDisposable Enter(NpcWakeContext context, INpcThinkClock clock, bool enabled)
    {
        Execution? previous = CurrentExecution.Value;
        CurrentExecution.Value = enabled ? new Execution(context, clock) : null;
        return new Scope(previous);
    }

    public static void CommandStarted()
    {
        Execution? execution = CurrentExecution.Value;
        if (execution == null || Interlocked.Exchange(ref execution.Recorded, 1) != 0)
        {
            return;
        }

        long now = execution.Clock.GetTimestamp();
        NpcAiTelemetry.RecordReactionLatency(execution.Context,
            execution.Clock.GetElapsedTime(execution.Context.EventTimestamp, now));
    }

    private sealed class Execution(NpcWakeContext context, INpcThinkClock clock)
    {
        public NpcWakeContext Context { get; } = context;
        public INpcThinkClock Clock { get; } = clock;
        public int Recorded;
    }

    private sealed class Scope(Execution? previous): IDisposable
    {
        public void Dispose() => CurrentExecution.Value = previous;
    }
}
