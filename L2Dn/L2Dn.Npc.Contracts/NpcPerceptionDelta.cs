using System.Collections.Immutable;

namespace L2Dn.NpcContracts;

[Flags]
public enum NpcPerceptionChangeMask
{
    None = 0,
    Identity = 1 << 0,
    Physical = 1 << 1,
    Combat = 1 << 2,
    Environment = 1 << 3,
    VisibleEntities = 1 << 4,
    Threats = 1 << 5,
    Affordances = 1 << 6,
    SpatialObservations = 1 << 7,
    Skills = 1 << 8
}

public sealed record NpcPerceptionDeltaEnvelope(
    int SchemaVersion,
    NpcKey Npc,
    long BaseRevision,
    long Revision,
    long WorldTick,
    long CaptureMonotonicMilliseconds);

public sealed record VisibleEntityDelta
{
    public VisibleEntityDelta(ImmutableArray<VisibleEntity> added, ImmutableArray<VisibleEntity> updated,
        ImmutableArray<EntityKey> removed)
    {
        Added = added.IsDefault ? [] : added;
        Updated = updated.IsDefault ? [] : updated;
        Removed = removed.IsDefault ? [] : removed;
    }

    public ImmutableArray<VisibleEntity> Added { get; }
    public ImmutableArray<VisibleEntity> Updated { get; }
    public ImmutableArray<EntityKey> Removed { get; }
}

public sealed record NpcPerceptionDelta
{
    public NpcPerceptionDelta(
        NpcPerceptionDeltaEnvelope envelope,
        NpcPerceptionChangeMask changes,
        NpcIdentity? identity,
        NpcPhysicalState? physical,
        NpcCombatFacts? combat,
        NpcEnvironment? environment,
        VisibleEntityDelta? visibleEntities,
        ImmutableArray<ThreatEntry> threats,
        ImmutableArray<NpcAffordanceObservation> affordances,
        ImmutableArray<SpatialObservation> spatialObservations,
        ImmutableArray<NpcSkillObservation> skills = default)
    {
        Envelope = envelope;
        Changes = changes;
        Identity = identity;
        Physical = physical;
        Combat = combat;
        Environment = environment;
        VisibleEntities = visibleEntities;
        Threats = threats.IsDefault ? [] : threats;
        Affordances = affordances.IsDefault ? [] : affordances;
        SpatialObservations = spatialObservations.IsDefault ? [] : spatialObservations;
        Skills = skills.IsDefault ? [] : skills;
    }

    public NpcPerceptionDeltaEnvelope Envelope { get; }
    public NpcPerceptionChangeMask Changes { get; }
    public NpcIdentity? Identity { get; }
    public NpcPhysicalState? Physical { get; }
    public NpcCombatFacts? Combat { get; }
    public NpcEnvironment? Environment { get; }
    public VisibleEntityDelta? VisibleEntities { get; }
    public ImmutableArray<ThreatEntry> Threats { get; }
    public ImmutableArray<NpcAffordanceObservation> Affordances { get; }
    public ImmutableArray<SpatialObservation> SpatialObservations { get; }
    public ImmutableArray<NpcSkillObservation> Skills { get; }
}

public enum NpcPerceptionApplyStatus
{
    Applied = 0,
    SchemaMismatch = 1,
    NpcMismatch = 2,
    GenerationMismatch = 3,
    RevisionGap = 4,
    InvalidDelta = 5
}

public readonly record struct NpcPerceptionApplyResult(
    NpcPerceptionApplyStatus Status,
    NpcPerceptionSnapshot? Snapshot)
{
    public bool Applied => Status == NpcPerceptionApplyStatus.Applied;
}
