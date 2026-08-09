using System.Collections.Immutable;

namespace L2Dn.NpcContracts;

/// <summary>
/// Partial updates for one region produced by one legacy scheduler pool. This is not a complete region frame.
/// </summary>
public sealed record NpcPerceptionRegionBatch
{
    public NpcPerceptionRegionBatch(long worldTick, RegionKey region, int sourcePoolId, long batchSequence,
        ImmutableArray<NpcPerceptionSnapshot> fullSnapshots, ImmutableArray<NpcPerceptionDelta> deltas)
    {
        WorldTick = worldTick;
        Region = region;
        SourcePoolId = sourcePoolId;
        BatchSequence = batchSequence;
        FullSnapshots = fullSnapshots.IsDefault ? [] : fullSnapshots;
        Deltas = deltas.IsDefault ? [] : deltas;
    }

    public long WorldTick { get; }
    public RegionKey Region { get; }
    public int SourcePoolId { get; }
    public long BatchSequence { get; }
    public ImmutableArray<NpcPerceptionSnapshot> FullSnapshots { get; }
    public ImmutableArray<NpcPerceptionDelta> Deltas { get; }
}
