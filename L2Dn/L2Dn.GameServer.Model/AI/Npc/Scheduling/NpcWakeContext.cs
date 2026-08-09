using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Scheduling;

public readonly record struct NpcWakeContext(
    NpcKey Npc,
    NpcWakeReason Reasons,
    NpcThinkPriority Priority,
    long EventTimestamp,
    long WakeTimestamp,
    long ThinkStartedTimestamp,
    int SourcePoolId);

public enum NpcWakeDisposition
{
    Ignored = 0,
    Observed = 1,
    Queued = 2,
    Coalesced = 3,
    Deferred = 4,
    DroppedNormal = 5,
    StaleGeneration = 6
}
