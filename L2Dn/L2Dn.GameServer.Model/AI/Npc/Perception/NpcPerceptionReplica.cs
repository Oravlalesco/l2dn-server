using System.Collections.Concurrent;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal enum NpcPerceptionReplicaStatus
{
    Applied = 0,
    FullSnapshotRequired = 1,
    Rejected = 2
}

internal readonly record struct NpcPerceptionReplicaResult(
    NpcPerceptionReplicaStatus Status,
    NpcPerceptionApplyStatus? DeltaStatus,
    NpcPerceptionSnapshot? Snapshot);

/// <summary>
/// Small in-process reference receiver used to enforce the same gap recovery semantics expected from a remote brain.
/// </summary>
internal sealed class NpcPerceptionReplica
{
    private readonly ConcurrentDictionary<int, NpcPerceptionSnapshot> _snapshots = new();

    public NpcPerceptionReplicaResult ApplyFull(NpcPerceptionSnapshot snapshot)
    {
        if (snapshot.Envelope.SchemaVersion != NpcPerceptionSnapshot.CurrentSchemaVersion ||
            snapshot.Envelope.StateRevision <= 0)
        {
            return new NpcPerceptionReplicaResult(NpcPerceptionReplicaStatus.Rejected, null, null);
        }

        _snapshots[snapshot.Envelope.Npc.ObjectId] = snapshot;
        return new NpcPerceptionReplicaResult(NpcPerceptionReplicaStatus.Applied, null, snapshot);
    }

    public NpcPerceptionReplicaResult ApplyDelta(NpcPerceptionDelta delta)
    {
        if (!_snapshots.TryGetValue(delta.Envelope.Npc.ObjectId, out NpcPerceptionSnapshot? current))
        {
            NpcAiTelemetry.RecordPerceptionRevisionGap();
            return new NpcPerceptionReplicaResult(NpcPerceptionReplicaStatus.FullSnapshotRequired,
                NpcPerceptionApplyStatus.RevisionGap, null);
        }

        NpcPerceptionApplyResult applied = NpcPerceptionDeltaApplier.Apply(current, delta);
        if (applied.Applied)
        {
            _snapshots[delta.Envelope.Npc.ObjectId] = applied.Snapshot!;
            return new NpcPerceptionReplicaResult(NpcPerceptionReplicaStatus.Applied, applied.Status,
                applied.Snapshot);
        }

        if (applied.Status is NpcPerceptionApplyStatus.RevisionGap or
            NpcPerceptionApplyStatus.GenerationMismatch)
        {
            _snapshots.TryRemove(delta.Envelope.Npc.ObjectId, out _);
            NpcAiTelemetry.RecordPerceptionRevisionGap();
            return new NpcPerceptionReplicaResult(NpcPerceptionReplicaStatus.FullSnapshotRequired,
                applied.Status, null);
        }

        return new NpcPerceptionReplicaResult(NpcPerceptionReplicaStatus.Rejected, applied.Status, null);
    }

    public NpcPerceptionSnapshot? GetCurrent(int objectId) => _snapshots.GetValueOrDefault(objectId);
}
