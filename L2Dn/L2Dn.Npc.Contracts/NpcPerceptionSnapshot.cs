using System.Collections.Immutable;

namespace L2Dn.NpcContracts;

public sealed record NpcPerceptionEnvelope(
    int SchemaVersion,
    NpcKey Npc,
    long StateRevision,
    long WorldTick,
    long CaptureMonotonicMilliseconds);

public sealed record NpcIdentity
{
    public NpcIdentity(int templateId, NpcKind kind, LegacyNpcAiType legacyAiType, int level,
        int clanHelpRange, ImmutableArray<int> clanIds, NpcCapabilities capabilities)
    {
        TemplateId = templateId;
        Kind = kind;
        LegacyAiType = legacyAiType;
        Level = level;
        ClanHelpRange = clanHelpRange;
        ClanIds = clanIds.IsDefault ? [] : clanIds;
        Capabilities = capabilities;
    }

    public int TemplateId { get; }
    public NpcKind Kind { get; }
    public LegacyNpcAiType LegacyAiType { get; }
    public int Level { get; }
    public int ClanHelpRange { get; }
    public ImmutableArray<int> ClanIds { get; }
    public NpcCapabilities Capabilities { get; }
}

public sealed record NpcPhysicalState(
    NpcPosition Position,
    double CurrentHp,
    double MaximumHp,
    double CurrentMp,
    double MaximumMp,
    double CollisionRadius,
    double CollisionHeight,
    NpcPhysicalFlags Flags);

public sealed record NpcCombatFacts(
    EntityKey? CurrentTarget,
    int PhysicalAttackRange,
    int AggroRange,
    NpcCombatFlags Flags);

public sealed record NpcEnvironment(
    RegionKey Region,
    NpcPosition? SpawnPosition,
    bool RegionActive,
    bool NeighborsActive,
    bool RandomWalkingEnabled,
    bool ReturningToSpawn,
    bool CanReturnToSpawn);

public sealed record NpcPerceptionState
{
    public NpcPerceptionState(NpcIdentity identity, NpcPhysicalState physical, NpcCombatFacts combat,
        NpcEnvironment environment, ImmutableArray<VisibleEntity> visibleEntities,
        ImmutableArray<ThreatEntry> threats, ImmutableArray<NpcAffordanceObservation> affordances,
        ImmutableArray<SpatialObservation> spatialObservations)
    {
        Identity = identity;
        Physical = physical;
        Combat = combat;
        Environment = environment;
        VisibleEntities = visibleEntities.IsDefault ? [] : visibleEntities;
        Threats = threats.IsDefault ? [] : threats;
        Affordances = affordances.IsDefault ? [] : affordances;
        SpatialObservations = spatialObservations.IsDefault ? [] : spatialObservations;
    }

    public NpcIdentity Identity { get; }
    public NpcPhysicalState Physical { get; }
    public NpcCombatFacts Combat { get; }
    public NpcEnvironment Environment { get; }
    public ImmutableArray<VisibleEntity> VisibleEntities { get; }
    public ImmutableArray<ThreatEntry> Threats { get; }
    public ImmutableArray<NpcAffordanceObservation> Affordances { get; }
    public ImmutableArray<SpatialObservation> SpatialObservations { get; }
}

public sealed record NpcPerceptionSnapshot(NpcPerceptionEnvelope Envelope, NpcPerceptionState State)
{
    public const int CurrentSchemaVersion = 1;
}
