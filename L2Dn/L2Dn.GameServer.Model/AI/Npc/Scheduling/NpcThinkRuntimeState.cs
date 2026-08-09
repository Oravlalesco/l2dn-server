using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Scheduling;

internal enum NpcThinkQueueLocation
{
    None = 0,
    Active = 1,
    Delayed = 2,
    Overflow = 3
}

internal sealed class NpcThinkRuntimeState(NpcKey npc)
{
    public object SyncRoot { get; } = new();
    public NpcKey Npc { get; } = npc;
    public bool IsRunning { get; set; }
    public bool IsQueued { get; set; }
    public bool Removed { get; set; }
    public long QueueVersion { get; set; }
    public NpcThinkQueueLocation QueueLocation { get; set; }
    public NpcThinkPriority QueuedPriority { get; set; }
    public NpcWakeReason PendingReasons { get; set; }
    public NpcThinkPriority PendingPriority { get; set; }
    public long EarliestEventTimestamp { get; set; }
    public long EarliestWakeTimestamp { get; set; }
    public long LastThinkStarted { get; set; }
    public long LastThinkCompleted { get; set; }
    public int SourcePoolId { get; set; }
    public NpcWakeReason ObservedReasons { get; set; }
    public long EarliestObservedEventTimestamp { get; set; }
}

internal readonly record struct NpcThinkQueueEntry(NpcThinkRuntimeState State, long Version);

internal readonly record struct NpcDelayedThinkEntry(
    NpcThinkRuntimeState State,
    long Version,
    long DueTimestamp);

