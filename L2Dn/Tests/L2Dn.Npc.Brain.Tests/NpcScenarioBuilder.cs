using System.Collections.Immutable;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

// ---------------------------------------------------------------------------
// NpcScenarioBuilder — VAL-01
// Fluent builder for NpcPerceptionSnapshot used across all archetype tests.
//
// Design invariants:
//   1. SchemaVersion = 1 (NpcIntent.CurrentSchemaVersion) — Coordinator rejects
//      any other value silently.
//   2. StateRevision >= 1 — Coordinator returns empty decision at revision 0.
//   3. NpcPhysicalFlags.Alive | Spawned by default — IsActorOperational() requires
//      both; without them every Decide() returns 0 intents (tests would vacuously pass).
//   4. ThreatEntry.ValidTarget = true by default — SelectHighestVisibleThreat uses
//      `entry.ValidTarget && entry.Visible && entry.Hate > highestHate`; false
//      causes threats to be silently skipped.
//   5. NpcSkillObservationFlags default = Ready (NO Magic) — HasReadyPhysicalMeleeSkill
//      filters `!Magic`; passing Magic to a melee-category skill would make the skill
//      invisible to the physical path.
// ---------------------------------------------------------------------------

/// <summary>
/// Fluent builder for <see cref="NpcPerceptionSnapshot"/>.
/// Factories preset the <see cref="LegacyNpcAiType"/> and <see cref="NpcCapabilities"/>
/// that drive intelligence/strategy resolution.
/// </summary>
public sealed class NpcScenarioBuilder
{
    // -----------------------------------------------------------------------
    // Default constants
    // -----------------------------------------------------------------------
    private const int DefaultTemplateId = 100;
    private const int DefaultLevel = 20;
    private const int DefaultAggroRange = 500;
    private const int DefaultPhysicalAttackRange = 40;
    private const int DefaultCombatLeashDistance = 1500;
    private const int DefaultReturnHomeDistance = 300;
    private const double DefaultCollisionRadius = 8;
    private const double DefaultCollisionHeight = 16;
    private const double DefaultMaxHp = 100;
    private const long DefaultStateRevision = 1;

    private static readonly NpcKey DefaultActor = new(8172, 4);
    private static readonly EntityKey DefaultPlayer = new(9182, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // Fields
    // -----------------------------------------------------------------------
    private NpcKey _actor = DefaultActor;
    private LegacyNpcAiType _legacyAiType = LegacyNpcAiType.Fighter;
    private NpcCapabilities _capabilities = NpcCapabilities.CanMove | NpcCapabilities.CanAttack |
        NpcCapabilities.Aggressive;
    private NpcKind _kind = NpcKind.Monster;
    private NpcPhysicalFlags _physicalFlags = NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned;
    private NpcCombatFlags _combatFlags = NpcCombatFlags.None;
    private NpcPosition _position = new(0, 0, 0, 0);
    private NpcPosition? _spawnPosition = new(0, 0, 0, 0);
    private EntityKey? _currentTarget;
    private bool _returningToSpawn;
    private double _currentHp = DefaultMaxHp;
    private double _maxHp = DefaultMaxHp;
    private int _combatLeashDistance = DefaultCombatLeashDistance;
    private int _aggroRange = DefaultAggroRange;
    private int _physicalAttackRange = DefaultPhysicalAttackRange;
    private long _stateRevision = DefaultStateRevision;

    private readonly List<VisibleEntity> _visible = [];
    private readonly List<ThreatEntry> _threats = [];
    private readonly List<NpcSkillObservation> _skills = [];

    // -----------------------------------------------------------------------
    // Archetype factories
    // Note: NpcIntelligenceProfileResolver resolves profile from LegacyAiType
    // + capabilities, NOT from CanMove/CanAttack/CanCast individually.
    // CanMove/CanAttack/CanCast are informational only. The key discriminators
    // are LegacyAiType (Fighter→Melee, Mage/Healer→Caster) and the Aggressive flag.
    // -----------------------------------------------------------------------

    /// <summary>Fighter-class melee NPC (aggressive, Melee intelligence profile).</summary>
    public static NpcScenarioBuilder CreateMelee() => new NpcScenarioBuilder()
        .SetAiType(LegacyNpcAiType.Fighter)
        .SetCapabilities(NpcCapabilities.CanMove | NpcCapabilities.CanAttack | NpcCapabilities.Aggressive);

    /// <summary>
    /// Archer-class NPC. Resolves internally to Aggressive intelligence profile (rango 0)
    /// because LegacyAiType.Archer is not Mage/Healer/Fighter. Tests that need ranged
    /// behavior must call DecideWithStrategy(…, NpcStrategyProfileResolver.RangedControl).
    /// </summary>
    public static NpcScenarioBuilder CreateArcher() => new NpcScenarioBuilder()
        .SetAiType(LegacyNpcAiType.Archer)
        .SetCapabilities(NpcCapabilities.CanMove | NpcCapabilities.CanAttack | NpcCapabilities.Aggressive);

    /// <summary>Mage-class NPC (Caster intelligence profile, PreferredRange 400).</summary>
    public static NpcScenarioBuilder CreateMage() => new NpcScenarioBuilder()
        .SetAiType(LegacyNpcAiType.Mage)
        .SetCapabilities(NpcCapabilities.CanMove | NpcCapabilities.CanAttack | NpcCapabilities.CanCast |
            NpcCapabilities.Aggressive);

    /// <summary>Guard NPC — can acquire targets in peace zones.</summary>
    public static NpcScenarioBuilder CreateGuard() => new NpcScenarioBuilder()
        .SetAiType(LegacyNpcAiType.Fighter)
        .SetKind(NpcKind.Guard)
        .SetCapabilities(NpcCapabilities.CanMove | NpcCapabilities.CanAttack | NpcCapabilities.Guard |
            NpcCapabilities.CanAcquireInPeaceZone);

    /// <summary>Passive NPC — no Aggressive capability; does not auto-acquire hostiles.</summary>
    public static NpcScenarioBuilder CreatePassive() => new NpcScenarioBuilder()
        .SetAiType(LegacyNpcAiType.Fighter)
        .SetCapabilities(NpcCapabilities.CanMove | NpcCapabilities.CanAttack);

    /// <summary>Raid boss NPC (aggressive + RaidBoss capability).</summary>
    public static NpcScenarioBuilder CreateRaidBoss() => new NpcScenarioBuilder()
        .SetAiType(LegacyNpcAiType.Fighter)
        .SetKind(NpcKind.RaidBoss)
        .SetCapabilities(NpcCapabilities.CanMove | NpcCapabilities.CanAttack | NpcCapabilities.Aggressive |
            NpcCapabilities.RaidBoss);

    // -----------------------------------------------------------------------
    // Actor state modifiers
    // -----------------------------------------------------------------------

    public NpcScenarioBuilder WithHp(double ratio)
    {
        double clamped = Math.Clamp(ratio, 0.0, 1.0);
        _currentHp = _maxHp * clamped;
        return this;
    }

    public NpcScenarioBuilder WithPosition(int x, int y, int z)
    {
        _position = new NpcPosition(x, y, z, 0);
        return this;
    }

    public NpcScenarioBuilder WithSpawnPosition(int x, int y, int z)
    {
        _spawnPosition = new NpcPosition(x, y, z, 0);
        return this;
    }

    /// <summary>
    /// Replaces the physical flags entirely. Default is <c>Alive | Spawned</c>.
    /// If you override this, ensure Alive + Spawned are set unless you explicitly
    /// want to test dead/unspawned behavior.
    /// </summary>
    public NpcScenarioBuilder WithPhysicalFlags(NpcPhysicalFlags flags)
    {
        _physicalFlags = flags;
        return this;
    }

    public NpcScenarioBuilder WithCombatFlags(NpcCombatFlags flags)
    {
        _combatFlags = flags;
        return this;
    }

    public NpcScenarioBuilder WithCombatLeashDistance(int units)
    {
        _combatLeashDistance = units;
        return this;
    }

    public NpcScenarioBuilder WithCurrentTarget(EntityKey key)
    {
        _currentTarget = key;
        return this;
    }

    public NpcScenarioBuilder ReturningToSpawn()
    {
        _returningToSpawn = true;
        return this;
    }

    // -----------------------------------------------------------------------
    // Visible entities
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds a hostile entity at the given 2D distance. Marks it as
    /// AutoAttackable + Playable + SameInstance + Alive + Spawned.
    /// </summary>
    public NpcScenarioBuilder WithHostileAt(double distance2D, int ordinal = 0, bool moving = false)
        => WithHostileAt(DefaultPlayer, distance2D, ordinal, moving);

    public NpcScenarioBuilder WithHostileAt(EntityKey key, double distance2D, int ordinal = 0,
        bool moving = false)
    {
        _visible.Add(new VisibleEntity(
            ordinal, key,
            new NpcPosition((int)distance2D, 0, 0, 0),
            DefaultLevel, DefaultCollisionRadius, DefaultCollisionHeight, distance2D,
            EntityStateFlags.Alive | EntityStateFlags.Spawned |
                (moving ? EntityStateFlags.Moving : EntityStateFlags.None),
            EntityRelationFlags.Player | EntityRelationFlags.Playable |
                EntityRelationFlags.SameInstance | EntityRelationFlags.AutoAttackable));
        return this;
    }

    /// <summary>Adds a hostile entity flagged as actively moving toward the actor.</summary>
    public NpcScenarioBuilder WithHostileApproaching(double distance2D, int ordinal = 0)
        => WithHostileAt(DefaultPlayer, distance2D, ordinal, moving: true);

    /// <summary>
    /// Adds a hostile entity protected by a peace zone (PeaceZone + NoPvpZone).
    /// Aggressive mobs cannot acquire this target unless CanAcquireInPeaceZone is set.
    /// </summary>
    public NpcScenarioBuilder WithEntityInPeaceZone(double distance2D, int ordinal = 0)
    {
        _visible.Add(new VisibleEntity(
            ordinal, DefaultPlayer,
            new NpcPosition((int)distance2D, 0, 0, 0),
            DefaultLevel, DefaultCollisionRadius, DefaultCollisionHeight, distance2D,
            EntityStateFlags.Alive | EntityStateFlags.Spawned |
                EntityStateFlags.PeaceZone | EntityStateFlags.NoPvpZone,
            EntityRelationFlags.Player | EntityRelationFlags.Playable |
                EntityRelationFlags.SameInstance | EntityRelationFlags.AutoAttackable));
        return this;
    }

    public NpcScenarioBuilder WithAllyAt(EntityKey key, double distance2D, int ordinal = 0)
    {
        _visible.Add(new VisibleEntity(
            ordinal, key,
            new NpcPosition((int)distance2D, 0, 0, 0),
            DefaultLevel, DefaultCollisionRadius, DefaultCollisionHeight, distance2D,
            EntityStateFlags.Alive | EntityStateFlags.Spawned,
            EntityRelationFlags.SameInstance | EntityRelationFlags.SameClan));
        return this;
    }

    // -----------------------------------------------------------------------
    // Threat table
    // Note: ValidTarget defaults to true. A false ValidTarget causes
    // SelectHighestVisibleThreat to silently skip the entry.
    // -----------------------------------------------------------------------

    public NpcScenarioBuilder WithThreatEntry(EntityKey target, long hate, long damage,
        double distance, bool visible = true, bool validTarget = true)
    {
        _threats.Add(new ThreatEntry(target, hate, damage, distance, visible, validTarget));
        return this;
    }

    /// <summary>
    /// Adds multiple threat entries for the default player key.
    /// Each tuple is (hate, distance); damage defaults to hate/2.
    /// All entries use Visible=true, ValidTarget=true.
    /// </summary>
    public NpcScenarioBuilder WithThreatEntries(params (EntityKey target, long hate, double distance)[] entries)
    {
        foreach ((EntityKey target, long hate, double distance) in entries)
        {
            _threats.Add(new ThreatEntry(target, hate, hate / 2, distance, visible: true, validTarget: true));
        }

        return this;
    }

    // -----------------------------------------------------------------------
    // Skills
    // Note: Default flags = Ready (NOT Magic). This matches HasReadyPhysicalMeleeSkill
    // which requires !Magic for physical/melee skills. For magic skills, callers
    // must explicitly pass NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds a ready skill. Default flags = <c>Ready</c> (no Magic) for melee/physical skills.
    /// For magic skills, pass <c>NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic</c>.
    /// </summary>
    public NpcScenarioBuilder WithSkillReady(int id, NpcSkillCategory category, int range = 0,
        int mpCost = 0, NpcSkillObservationFlags flags = NpcSkillObservationFlags.Ready)
    {
        _skills.Add(new NpcSkillObservation(id, level: 1, range, mpCost, category, flags));
        return this;
    }

    public NpcScenarioBuilder WithSkillOnCooldown(int id, NpcSkillCategory category, int range = 0)
    {
        _skills.Add(new NpcSkillObservation(id, level: 1, range, mpCost: 0, category,
            NpcSkillObservationFlags.Cooldown));
        return this;
    }

    // -----------------------------------------------------------------------
    // Capability overrides
    // -----------------------------------------------------------------------

    public NpcScenarioBuilder WithCapabilities(NpcCapabilities extra)
    {
        _capabilities |= extra;
        return this;
    }

    public NpcScenarioBuilder WithoutCapabilities(NpcCapabilities remove)
    {
        _capabilities &= ~remove;
        return this;
    }

    // -----------------------------------------------------------------------
    // Build
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds an immutable <see cref="NpcPerceptionSnapshot"/>.
    /// SchemaVersion is always 1 and StateRevision is always >= 1 to satisfy
    /// <see cref="NpcBrainCoordinator"/> invariants.
    /// </summary>
    public NpcPerceptionSnapshot Build()
    {
        NpcIdentity identity = new(
            DefaultTemplateId, _kind, _legacyAiType, DefaultLevel,
            clanHelpRange: 0, clanIds: [], _capabilities);

        NpcPhysicalState physical = new(
            _position, _currentHp, _maxHp,
            currentMp: 50, maximumMp: 50,
            DefaultCollisionRadius, DefaultCollisionHeight, _physicalFlags);

        NpcCombatFacts combat = new(_currentTarget, _physicalAttackRange, _aggroRange, _combatFlags);

        NpcEnvironment environment = new(
            new RegionKey(0, 1, 1), _spawnPosition,
            regionActive: true, neighborsActive: true,
            randomWalkingEnabled: false, _returningToSpawn,
            canReturnToSpawn: true, DefaultReturnHomeDistance, _combatLeashDistance);

        NpcPerceptionState state = new(
            identity, physical, combat, environment,
            _visible.ToImmutableArray(),
            _threats.ToImmutableArray(),
            affordances: [],
            spatialObservations: [],
            _skills.ToImmutableArray());

        // SchemaVersion = 1 (CurrentSchemaVersion) and StateRevision >= 1 are
        // invariants; without them Decide() returns empty silently.
        return new NpcPerceptionSnapshot(
            new NpcPerceptionEnvelope(
                SchemaVersion: 1,
                _actor,
                StateRevision: Math.Max(1, _stateRevision),
                WorldTick: Math.Max(1, _stateRevision),
                CaptureMonotonicMilliseconds: Math.Max(1, _stateRevision)),
            state);
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private NpcScenarioBuilder SetAiType(LegacyNpcAiType type) { _legacyAiType = type; return this; }
    private NpcScenarioBuilder SetKind(NpcKind kind) { _kind = kind; return this; }
    private NpcScenarioBuilder SetCapabilities(NpcCapabilities caps) { _capabilities = caps; return this; }
}

// ---------------------------------------------------------------------------
// ScenarioContext — contextual stimuli helpers colocated with the builder
// ---------------------------------------------------------------------------

/// <summary>
/// Factory helpers for <see cref="NpcBrainContext"/> used alongside <see cref="NpcScenarioBuilder"/>.
/// </summary>
public static class ScenarioContext
{
    public static NpcBrainContext Periodic()      => new(NpcBrainStimulus.PeriodicDue);
    public static NpcBrainContext Aggro()         => new(NpcBrainStimulus.PlayerBecameRelevant);
    public static NpcBrainContext Attacked()      => new(NpcBrainStimulus.Attacked);
    public static NpcBrainContext ThreatChanged() => new(NpcBrainStimulus.ThreatChanged);
    public static NpcBrainContext TargetDied()    => new(NpcBrainStimulus.TargetDied);
    public static NpcBrainContext CombatStarted() => new(NpcBrainStimulus.CombatStarted);
    public static NpcBrainContext ActionReady()   => new(NpcBrainStimulus.ActionReady);
}
