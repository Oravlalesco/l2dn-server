using System.Collections.Immutable;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.Skills;
using L2Dn.GameServer.Model.Zones;
using L2Dn.GameServer.Utilities;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

/// <summary>
/// Read-only translation boundary from mutable GameServer objects to immutable NPC contracts.
/// Capture must remain observationally pure: it may not mutate hate, targets, movement, AI state or the world.
/// </summary>
internal sealed class NpcPerceptionBuilder
{
    private const int MaxCaptureAttempts = 2;

    private readonly INpcWorldQuery _world;
    private readonly INpcGeoQuery _geo;
    private readonly INpcThreatQuery _threat;
    private readonly INpcPerceptionClock _clock;

    public NpcPerceptionBuilder(INpcWorldQuery world, INpcGeoQuery geo, INpcThreatQuery threat,
        INpcPerceptionClock? clock = null)
    {
        _world = world;
        _geo = geo;
        _threat = threat;
        _clock = clock ?? SystemNpcPerceptionClock.Instance;
    }

    public bool TryCapture(Attackable actor, out NpcPerceptionCapture? capture)
    {
        for (int attempt = 0; attempt < MaxCaptureAttempts; attempt++)
        {
            if (TryCaptureOnce(actor, out capture))
            {
                return true;
            }
        }

        capture = null;
        return false;
    }

    private bool TryCaptureOnce(Attackable actor, out NpcPerceptionCapture? capture)
    {
        if (!actor.tryGetStableLifecycleStamp(out NpcLifecycleStamp before))
        {
            capture = null;
            return false;
        }

        long worldTick = _world.GetWorldTick();
        long capturedAt = _clock.GetMonotonicMilliseconds();

        // Threat is an independent read model. It must not be derived from current visibility.
        List<AggroObservation> liveThreats = _threat.GetAggroEntries(actor)
            .Select(static entry => new AggroObservation(entry.getAttacker(), entry.getHate(), entry.getDamage()))
            .ToList();
        Creature? primaryThreat = liveThreats
            .Where(static entry => entry.Hate > 0)
            .OrderByDescending(static entry => entry.Hate)
            .Select(static entry => entry.Attacker)
            .FirstOrDefault();

        // Preserve the legacy enumeration order as the observation ordinal.
        List<WorldObject> visibleObjects = _world.GetVisibleObjects<WorldObject>(actor);
        ImmutableArray<VisibleEntity>.Builder visible = ImmutableArray.CreateBuilder<VisibleEntity>(visibleObjects.Count);
        WorldObject? currentTarget = actor.getTarget();
        for (int ordinal = 0; ordinal < visibleObjects.Count; ordinal++)
        {
            WorldObject observed = visibleObjects[ordinal];
            visible.Add(MapVisibleEntity(actor, observed, ordinal, currentTarget, primaryThreat));
        }

        HashSet<int> visibleIds = visibleObjects.Select(static entity => entity.ObjectId).ToHashSet();
        ImmutableArray<ThreatEntry>.Builder threats = ImmutableArray.CreateBuilder<ThreatEntry>(liveThreats.Count);
        foreach (AggroObservation threat in liveThreats)
        {
            Creature target = threat.Attacker;
            threats.Add(new ThreatEntry(
                ToEntityKey(target),
                threat.Hate,
                threat.Damage,
                Distance2D(actor, target),
                visibleIds.Contains(target.ObjectId),
                IsValidThreatTarget(actor, target)));
        }

        List<WorldObject> enrichedCandidates = [];
        AddCandidate(enrichedCandidates, currentTarget);
        AddCandidate(enrichedCandidates, primaryThreat);
        ImmutableArray<NpcAffordanceObservation>.Builder affordances =
            ImmutableArray.CreateBuilder<NpcAffordanceObservation>(enrichedCandidates.Count);
        ImmutableArray<SpatialObservation>.Builder spatial =
            ImmutableArray.CreateBuilder<SpatialObservation>(enrichedCandidates.Count);
        foreach (WorldObject candidate in enrichedCandidates)
        {
            EntityKey key = ToEntityKey(candidate);
            affordances.Add(new NpcAffordanceObservation(key, EvaluateAffordances(actor, candidate)));

            SpatialObservationFlags spatialFlags = SpatialObservationFlags.None;
            if (_geo.CanSeeTarget(actor, candidate))
            {
                spatialFlags |= SpatialObservationFlags.HasLineOfSight;
            }

            if (_geo.CanMoveToTarget(actor.Location.Location3D, candidate.Location.Location3D,
                    actor.getInstanceWorld()))
            {
                spatialFlags |= SpatialObservationFlags.CanReachDirectly;
            }

            spatial.Add(new SpatialObservation(key, spatialFlags));
        }

        NpcPerceptionState state = new(
            MapIdentity(actor),
            MapPhysical(actor),
            MapCombat(actor, currentTarget),
            MapEnvironment(actor),
            visible.MoveToImmutable(),
            threats.MoveToImmutable(),
            affordances.MoveToImmutable(),
            spatial.MoveToImmutable(),
            MapSkills(actor));

        if (!actor.tryGetStableLifecycleStamp(out NpcLifecycleStamp after) || after != before)
        {
            capture = null;
            return false;
        }

        capture = new NpcPerceptionCapture(
            new NpcKey(actor.ObjectId, before.Generation), before, worldTick, capturedAt, state);
        return true;
    }

    private static NpcIdentity MapIdentity(Attackable actor)
    {
        NpcTemplate template = actor.getTemplate();
        NpcCapabilities capabilities = NpcCapabilities.None;
        if (template.canMove()) capabilities |= NpcCapabilities.CanMove;
        if (template.isAttackable()) capabilities |= NpcCapabilities.CanAttack;
        if (template.getSkills().Count != 0) capabilities |= NpcCapabilities.CanCast;
        if (actor.isAggressive()) capabilities |= NpcCapabilities.Aggressive;
        if (actor is Guard) capabilities |= NpcCapabilities.Guard;
        if (actor.isRaid()) capabilities |= NpcCapabilities.RaidBoss;
        if (actor.isRaidMinion()) capabilities |= NpcCapabilities.RaidMinion;
        if (actor.isFlying()) capabilities |= NpcCapabilities.Flying;
        if (actor.isFakePlayer()) capabilities |= NpcCapabilities.FakePlayer;
        if (actor.canSeeThroughSilentMove()) capabilities |= NpcCapabilities.CanSeeSilentMovement;

        return new NpcIdentity(
            actor.getId(),
            GetNpcKind(actor),
            GetLegacyAiType(template.getAIType()),
            actor.getLevel(),
            template.getClanHelpRange(),
            template.getClans().ToImmutableArray(),
            capabilities);
    }

    private static NpcPhysicalState MapPhysical(Attackable actor)
    {
        NpcPhysicalFlags flags = NpcPhysicalFlags.None;
        if (!actor.isDead()) flags |= NpcPhysicalFlags.Alive;
        if (actor.isAlikeDead()) flags |= NpcPhysicalFlags.AlikeDead;
        if (actor.isRunning()) flags |= NpcPhysicalFlags.Running;
        if (actor.isMoving()) flags |= NpcPhysicalFlags.Moving;
        if (actor.isMovementDisabled()) flags |= NpcPhysicalFlags.MovementDisabled;
        if (actor.isSpawned()) flags |= NpcPhysicalFlags.Spawned;

        return new NpcPhysicalState(
            ToPosition(actor),
            actor.getCurrentHp(),
            actor.getMaxHp(),
            actor.getCurrentMp(),
            actor.getMaxMp(),
            actor.getCollisionRadius(),
            actor.getCollisionHeight(),
            flags);
    }

    private static NpcCombatFacts MapCombat(Attackable actor, WorldObject? currentTarget)
    {
        NpcCombatFlags flags = NpcCombatFlags.None;
        if (actor.isInCombat()) flags |= NpcCombatFlags.InCombat;
        if (actor.isCastingNow()) flags |= NpcCombatFlags.Casting;
        if (actor.isAttackingNow()) flags |= NpcCombatFlags.Attacking;
        if (actor.isConfused()) flags |= NpcCombatFlags.Confused;
        if (actor.isCoreAIDisabled()) flags |= NpcCombatFlags.CoreAiDisabled;
        if (actor.isAllSkillsDisabled()) flags |= NpcCombatFlags.AllSkillsDisabled;

        return new NpcCombatFacts(
            currentTarget == null ? null : ToEntityKey(currentTarget),
            actor.getPhysicalAttackRange(),
            actor.getAggroRange(),
            flags);
    }

    private static NpcEnvironment MapEnvironment(Attackable actor)
    {
        WorldRegion region = actor.getWorldRegion();
        Spawn? spawn = actor.getSpawn();
        NpcPosition? spawnPosition = spawn == null
            ? null
            : new NpcPosition(spawn.Location.X, spawn.Location.Y, spawn.Location.Z, spawn.Location.Heading);

        return new NpcEnvironment(
            new RegionKey(actor.getInstanceId(), region.RegionX, region.RegionY),
            spawnPosition,
            region.Active,
            region.AreNeighborsActive,
            actor.isRandomWalkingEnabled(),
            false,
            actor.canReturnToSpawnPoint());
    }

    private static ImmutableArray<NpcSkillObservation> MapSkills(Attackable actor)
    {
        Dictionary<(int Id, int Level), (Skill Skill, NpcSkillCategory Category)> observations = [];
        AddSkills(observations, actor, AISkillScope.HEAL, NpcSkillCategory.Heal);
        AddSkills(observations, actor, AISkillScope.RES, NpcSkillCategory.Resurrection);
        AddSkills(observations, actor, AISkillScope.BUFF, NpcSkillCategory.Buff);
        AddSkills(observations, actor, AISkillScope.DEBUFF, NpcSkillCategory.Debuff);
        AddSkills(observations, actor, AISkillScope.NEGATIVE, NpcSkillCategory.Debuff);
        AddSkills(observations, actor, AISkillScope.IMMOBILIZE, NpcSkillCategory.Control);
        AddSkills(observations, actor, AISkillScope.COT, NpcSkillCategory.Control);
        AddSkills(observations, actor, AISkillScope.SUICIDE, NpcSkillCategory.Suicide);
        AddSkills(observations, actor, AISkillScope.ATTACK, NpcSkillCategory.Offensive);
        AddSkills(observations, actor, AISkillScope.LONG_RANGE, NpcSkillCategory.Offensive);
        AddSkills(observations, actor, AISkillScope.SHORT_RANGE, NpcSkillCategory.Offensive);
        AddSkills(observations, actor, AISkillScope.GENERAL, NpcSkillCategory.Offensive);
        AddSkills(observations, actor, AISkillScope.UNIVERSAL, NpcSkillCategory.Offensive);

        return [.. observations.Values
            .OrderBy(static item => item.Skill.getId())
            .ThenBy(static item => item.Skill.getLevel())
            .Select(item => MapSkill(actor, item.Skill, item.Category))];
    }

    private static void AddSkills(
        Dictionary<(int Id, int Level), (Skill Skill, NpcSkillCategory Category)> observations,
        Attackable actor, AISkillScope scope, NpcSkillCategory category)
    {
        foreach (Skill skill in actor.getTemplate().getAISkills(scope))
        {
            observations.TryAdd((skill.getId(), skill.getLevel()), (skill, category));
        }
    }

    private static NpcSkillObservation MapSkill(Attackable actor, Skill skill, NpcSkillCategory category)
    {
        int mpCost = skill.getMpConsume() + skill.getMpInitialConsume();
        bool cooldown = actor.hasSkillReuse(skill.getReuseHashCode()) || actor.isSkillDisabled(skill);
        bool insufficientMana = actor.getCurrentMp() < mpCost;
        NpcSkillObservationFlags flags = NpcSkillObservationFlags.None;
        if (!cooldown && !insufficientMana && !actor.isAllSkillsDisabled())
            flags |= NpcSkillObservationFlags.Ready;
        if (cooldown) flags |= NpcSkillObservationFlags.Cooldown;
        if (insufficientMana) flags |= NpcSkillObservationFlags.InsufficientMana;
        if (skill.isMagic()) flags |= NpcSkillObservationFlags.Magic;
        if (skill.isBad()) flags |= NpcSkillObservationFlags.Bad;
        return new NpcSkillObservation(skill.getId(), skill.getLevel(), skill.getCastRange(), mpCost,
            category, flags);
    }

    private static VisibleEntity MapVisibleEntity(Attackable observer, WorldObject observed, int ordinal,
        WorldObject? currentTarget, Creature? primaryThreat)
    {
        EntityStateFlags state = EntityStateFlags.None;
        if (observed.isSpawned()) state |= EntityStateFlags.Spawned;
        if (observed.isInvul()) state |= EntityStateFlags.Invulnerable;

        int level = 0;
        double collisionRadius = 0;
        double collisionHeight = 0;
        if (observed is Creature creature)
        {
            level = creature.getLevel();
            collisionRadius = creature.getCollisionRadius();
            collisionHeight = creature.getCollisionHeight();
            if (!creature.isDead()) state |= EntityStateFlags.Alive;
            if (creature.isAlikeDead()) state |= EntityStateFlags.AlikeDead;
            if (creature.isMoving()) state |= EntityStateFlags.Moving;
            if (creature.isCastingNow()) state |= EntityStateFlags.Casting;
            if (creature is Playable playable && playable.isSilentMovingAffected())
                state |= EntityStateFlags.SilentMoving;
            if (creature is Player player && player.isRecentFakeDeath()) state |= EntityStateFlags.RecentFakeDeath;
            if (creature.isInsideZone(ZoneId.PEACE)) state |= EntityStateFlags.PeaceZone;
            if (creature.isInsideZone(ZoneId.NO_PVP)) state |= EntityStateFlags.NoPvpZone;
        }

        EntityRelationFlags relations = GetRelations(observer, observed);
        if (ReferenceEquals(observed, currentTarget)) relations |= EntityRelationFlags.CurrentTarget;
        if (ReferenceEquals(observed, primaryThreat)) relations |= EntityRelationFlags.PrimaryThreat;

        return new VisibleEntity(
            ordinal,
            ToEntityKey(observed),
            ToPosition(observed),
            level,
            collisionRadius,
            collisionHeight,
            Distance2D(observer, observed),
            state,
            relations);
    }

    private static EntityRelationFlags GetRelations(Attackable observer, WorldObject observed)
    {
        EntityRelationFlags result = EntityRelationFlags.None;
        if (observed.isPlayable()) result |= EntityRelationFlags.Playable;
        if (observed.isPlayer()) result |= EntityRelationFlags.Player;
        if (observed.isSummon()) result |= EntityRelationFlags.Summon;
        if (observed.isNpc()) result |= EntityRelationFlags.Npc;
        if (observed.isAttackable()) result |= EntityRelationFlags.Attackable;
        if (observed.isMonster()) result |= EntityRelationFlags.Monster;
        if (observed is Guard) result |= EntityRelationFlags.Guard;
        if (observed.isFakePlayer()) result |= EntityRelationFlags.FakePlayer;
        if (observed.getInstanceId() == observer.getInstanceId()) result |= EntityRelationFlags.SameInstance;
        if (observed is Npc npc && HaveSameClan(observer, npc)) result |= EntityRelationFlags.SameClan;
        return result;
    }

    private static NpcAffordanceFlags EvaluateAffordances(Attackable observer, WorldObject target)
    {
        bool sameInstance = observer.getInstanceId() == target.getInstanceId();
        bool targetable = sameInstance && target.isSpawned() && target.isTargetable();
        NpcAffordanceFlags result = targetable ? NpcAffordanceFlags.CanTarget : NpcAffordanceFlags.None;

        bool autoAttackable = targetable && target.isAutoAttackable(observer);
        if (autoAttackable) result |= NpcAffordanceFlags.AutoAttackable;
        if (autoAttackable && target is Creature creature && !creature.isAlikeDead())
            result |= NpcAffordanceFlags.CanPhysicallyAttack;
        if (targetable && target is Npc npc && HaveSameClan(observer, npc))
            result |= NpcAffordanceFlags.CanAssist;
        if (targetable && target is Npc) result |= NpcAffordanceFlags.CanInteract;
        return result;
    }

    private static bool IsValidThreatTarget(Attackable observer, Creature target) =>
        !target.isAlikeDead() && target.isSpawned() && observer.isInSurroundingRegion(target) &&
        observer.getInstanceId() == target.getInstanceId();

    private static bool HaveSameClan(Npc left, Npc right)
    {
        Set<int> leftClans = left.getTemplate().getClans();
        Set<int> rightClans = right.getTemplate().getClans();
        return leftClans.Count != 0 && rightClans.Count != 0 && leftClans.Any(rightClans.Contains);
    }

    private static NpcKind GetNpcKind(Attackable actor)
    {
        if (actor is Guard) return NpcKind.Guard;
        if (actor.isRaidMinion()) return NpcKind.RaidMinion;
        if (actor.isRaid()) return NpcKind.RaidBoss;
        if (actor is ControllableMob) return NpcKind.Controllable;
        if (actor is Monster) return NpcKind.Monster;
        return NpcKind.Attackable;
    }

    private static LegacyNpcAiType GetLegacyAiType(AIType type) => type switch
    {
        AIType.FIGHTER => LegacyNpcAiType.Fighter,
        AIType.ARCHER => LegacyNpcAiType.Archer,
        AIType.BALANCED => LegacyNpcAiType.Balanced,
        AIType.MAGE => LegacyNpcAiType.Mage,
        AIType.HEALER => LegacyNpcAiType.Healer,
        AIType.CORPSE => LegacyNpcAiType.Corpse,
        _ => LegacyNpcAiType.Unknown
    };

    private static EntityKey ToEntityKey(WorldObject entity) => new(
        entity.ObjectId,
        entity is Npc npc ? npc.getSpawnGeneration() : 0,
        GetEntityKind(entity));

    private static EntityKind GetEntityKind(WorldObject entity)
    {
        if (entity is Player) return EntityKind.Player;
        if (entity is Summon) return EntityKind.Summon;
        if (entity is Guard) return EntityKind.Guard;
        if (entity is Monster) return EntityKind.Monster;
        if (entity is Npc) return EntityKind.Npc;
        if (entity is Door) return EntityKind.Door;
        if (entity is Creature) return EntityKind.OtherCreature;
        return EntityKind.Unknown;
    }

    private static NpcPosition ToPosition(WorldObject entity) =>
        new(entity.getX(), entity.getY(), entity.getZ(), entity.getHeading());

    private static double Distance2D(WorldObject left, WorldObject right) =>
        double.Hypot((double)left.getX() - right.getX(), (double)left.getY() - right.getY());

    private static void AddCandidate(List<WorldObject> candidates, WorldObject? candidate)
    {
        if (candidate != null && candidates.All(existing => existing.ObjectId != candidate.ObjectId))
        {
            candidates.Add(candidate);
        }
    }

    private sealed record AggroObservation(Creature Attacker, long Hate, long Damage);
}
