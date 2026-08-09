using System.Collections.Immutable;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal static class NpcPerceptionBatchBuilder
{
    public static ImmutableArray<NpcPerceptionRegionBatch> Build(int sourcePoolId, long batchSequence,
        IEnumerable<NpcPerceptionCycle> cycles)
    {
        return cycles
            .Where(static cycle => cycle.Publication != NpcPerceptionPublicationKind.None)
            .GroupBy(static cycle => cycle.Snapshot.State.Environment.Region)
            .Select(group => new NpcPerceptionRegionBatch(
                group.Max(static cycle => cycle.Snapshot.Envelope.WorldTick),
                group.Key,
                sourcePoolId,
                batchSequence,
                group.Where(static cycle => cycle.Publication == NpcPerceptionPublicationKind.Full)
                    .Select(static cycle => cycle.Snapshot)
                    .ToImmutableArray(),
                group.Where(static cycle => cycle.Publication == NpcPerceptionPublicationKind.Delta)
                    .Select(static cycle => cycle.Delta!)
                    .ToImmutableArray()))
            .ToImmutableArray();
    }
}
