using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.GameServer.Model.Skills;
using L2Dn.Geometry;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.AI.Runtime;

internal sealed class NpcIntentGateway
{
    private readonly Func<int, WorldObject?> _resolve;
    private readonly Func<NpcKey, long?> _currentRevision;
    private readonly INpcGeoQuery _geo;
    private readonly ILegacyNpcCommandExecutor _commands;

    public static NpcIntentGateway Instance { get; } = new(
        static objectId => World.getInstance().findObject(objectId),
        static npc => NpcPerceptionCoordinator.Instance.TryGetPublishedRevision(npc, out long revision)
            ? revision
            : null,
        LegacyNpcGeoQuery.Instance,
        LegacyNpcCommandExecutor.Instance);

    internal NpcIntentGateway(Func<int, WorldObject?> resolve, Func<NpcKey, long?> currentRevision,
        INpcGeoQuery geo, ILegacyNpcCommandExecutor commands)
    {
        _resolve = resolve;
        _currentRevision = currentRevision;
        _geo = geo;
        _commands = commands;
    }

    public NpcIntentExecutionResult Execute(NpcIntent intent) =>
        NpcAiTelemetry.ObserveIntentExecution(intent, () => ExecuteCore(intent));

    private NpcIntentExecutionResult ExecuteCore(NpcIntent intent)
    {
        ArgumentNullException.ThrowIfNull(intent);
        NpcIntentEnvelope envelope = intent.Envelope;
        if (envelope.SchemaVersion != NpcIntent.CurrentSchemaVersion ||
            envelope.IntentType != GetIntentType(intent) || envelope.BasedOnStateRevision <= 0 ||
            envelope.DecisionSequence <= 0)
        {
            return Reject(NpcIntentRejectionReason.InvalidSchema);
        }

        WorldObject? resolvedActor = _resolve(envelope.Actor.ObjectId);
        if (resolvedActor == null)
        {
            return Reject(NpcIntentRejectionReason.ActorNotFound);
        }
        if (resolvedActor is not Attackable actor)
        {
            return Reject(NpcIntentRejectionReason.ActorUnsupported);
        }
        if (actor.getSpawnGeneration() != envelope.Actor.Generation)
        {
            return Reject(NpcIntentRejectionReason.GenerationMismatch);
        }
        if (!actor.isSpawned())
        {
            return Reject(NpcIntentRejectionReason.ActorNotSpawned);
        }
        if (actor.isAlikeDead())
        {
            return Reject(NpcIntentRejectionReason.ActorDead);
        }
        if (!actor.hasAI() || !NpcBrainEligibility.TryGetIntentAi(actor, out AttackableAI ai))
        {
            return Reject(NpcIntentRejectionReason.ActorUnsupported);
        }

        // A stale StateRevision is never trusted, but is not an automatic rejection. Every branch below
        // re-resolves and revalidates authoritative live objects before execution.
        _ = _currentRevision(envelope.Actor) is long current && current != envelope.BasedOnStateRevision;

        return intent switch
        {
            AcquireTargetIntent acquire => ExecuteAcquire(actor, ai, acquire),
            ClearTargetIntent clear => ExecuteClear(actor, ai, clear),
            BasicAttackIntent attack => ExecuteAttack(actor, ai, attack),
            ApproachTargetIntent approach => ExecuteApproach(actor, ai, approach),
            ReturnHomeIntent returnHome => ExecuteReturnHome(actor, ai, returnHome),
            FleeIntent flee => ExecuteFlee(actor, ai, flee),
            RetreatIntent retreat => ExecuteRetreat(actor, ai, retreat),
            CastSkillIntent cast => ExecuteCast(actor, cast),
            StopCombatIntent => ExecuteStopCombat(actor, ai),
            _ => Reject(NpcIntentRejectionReason.UnsupportedIntent)
        };
    }

    private NpcIntentExecutionResult ExecuteAcquire(Attackable actor, AttackableAI ai,
        AcquireTargetIntent intent)
    {
        if (intent.Mode is not (NpcTargetAcquisitionMode.Engage or
            NpcTargetAcquisitionMode.PreserveMovement))
        {
            return Reject(NpcIntentRejectionReason.InvalidIntent);
        }

        NpcIntentExecutionResult validation = ResolveLiveTarget(actor, intent.Target, true, out Creature? target);
        if (!validation.IsExecuted)
        {
            return validation;
        }

        if (actor.isCoreAIDisabled() || actor.isAllSkillsDisabled() || actor.isControlBlocked())
        {
            return Reject(NpcIntentRejectionReason.Policy);
        }

        Creature liveTarget = target!;
        // Fresh proximity acquisition must cross the same authoritative boundary
        // as legacy thinkActive(). Existing hate is retaliation and may legitimately
        // select an attacker outside the passive aggro radius.
        if (actor.getHating(liveTarget) <= 0)
        {
            int aggroRange = actor is Guard ? 500 : actor.getAggroRange();
            if (aggroRange <= 0 || !actor.IsInsideRadius3D(liveTarget, aggroRange))
            {
                return Reject(NpcIntentRejectionReason.OutOfRange);
            }
            if (!_geo.CanSeeTarget(actor, liveTarget))
            {
                return Reject(NpcIntentRejectionReason.Blocked);
            }
        }

        _commands.AddThreat(actor, liveTarget, 0, 1);
        if (intent.Mode == NpcTargetAcquisitionMode.PreserveMovement)
        {
            // Retargeting during a defensive return must not cancel the route
            // home. Tactical evaluation will attack, approach homeward, or keep
            // returning on the next single-flight Think.
            _commands.AbortAttack(actor);
            _commands.StopFollow(ai);
            _commands.SetTarget(actor, liveTarget);
            return NpcIntentExecutionResult.Executed();
        }

        if (!actor.isRunning())
        {
            _commands.SetRunning(actor);
        }

        // Acquiring a target must cancel any physical trajectory left by a
        // ReturnHomeIntent. Changing the AI intention alone does not stop a
        // MOVE_TO already in progress, so without this the NPC keeps walking
        // home until a later tactical evaluation starts following the attacker.
        _commands.StopFollow(ai);
        _commands.StopMovement(ai);
        _commands.SetIntention(ai, CtrlIntention.AI_INTENTION_ATTACK, liveTarget);
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ExecuteClear(Attackable actor, AttackableAI ai, ClearTargetIntent intent)
    {
        WorldObject? current = actor.getTarget();
        if (intent.ExpectedTarget is { } expected && current != null && current.ObjectId != expected.ObjectId)
        {
            return Reject(NpcIntentRejectionReason.TargetChanged);
        }

        if (current is Creature creature)
        {
            _commands.StopHating(actor, creature);
        }
        _commands.SetTarget(actor, null);
        _commands.SetIntention(ai, CtrlIntention.AI_INTENTION_ACTIVE);
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ExecuteAttack(Attackable actor, AttackableAI ai, BasicAttackIntent intent)
    {
        NpcIntentExecutionResult validation = ResolveLiveTarget(actor, intent.Target, true, out Creature? target);
        if (!validation.IsExecuted)
        {
            return validation;
        }
        if (actor.getTarget()?.ObjectId != target!.ObjectId)
        {
            return Reject(NpcIntentRejectionReason.TargetChanged);
        }
        if (actor.isAllSkillsDisabled() || actor.isCastingNow() || actor.isControlBlocked())
        {
            return Reject(NpcIntentRejectionReason.Policy);
        }

        int range = actor.getPhysicalAttackRange() + actor.getTemplate().getCollisionRadius() +
            target.getTemplate().getCollisionRadius();
        if (!actor.IsInsideRadius2D(target, range))
        {
            return Reject(NpcIntentRejectionReason.OutOfRange);
        }
        if (!_geo.CanSeeTarget(actor, target))
        {
            return Reject(NpcIntentRejectionReason.Blocked);
        }

        // Removal is idempotent. Always stop the legacy follow task before starting
        // an attack so it cannot overwrite the attack movement on its next tick.
        _commands.StopFollow(ai);
        if (!actor.isRunning())
        {
            _commands.SetRunning(actor);
        }
        _commands.AutoAttack(actor, target);
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ExecuteApproach(Attackable actor, AttackableAI ai,
        ApproachTargetIntent intent)
    {
        NpcIntentExecutionResult validation = ResolveLiveTarget(actor, intent.Target, false, out Creature? target);
        if (!validation.IsExecuted)
        {
            return validation;
        }
        if (actor.isMovementDisabled() || actor.isCastingNow())
        {
            return Reject(NpcIntentRejectionReason.Policy);
        }

        Creature liveTarget = target!;
        int stoppingRange = Math.Max(1, intent.PreferredRange);
        int collisionAdjustedRange = stoppingRange + (int)actor.getCollisionRadius() +
            (int)liveTarget.getCollisionRadius();
        if (actor.IsInsideRadius2D(liveTarget, collisionAdjustedRange))
        {
            return NpcIntentExecutionResult.Executed();
        }

        Location3D destination;
        if (intent.Constraint == NpcApproachConstraint.TowardSpawnOnly)
        {
            if (actor.getSpawn() is not { } spawn)
            {
                return Reject(NpcIntentRejectionReason.Policy);
            }

            double actorSpawnDistance = Distance2D(actor.getX(), actor.getY(),
                spawn.Location.X, spawn.Location.Y);
            double targetSpawnDistance = Distance2D(liveTarget.getX(), liveTarget.getY(),
                spawn.Location.X, spawn.Location.Y);
            if (targetSpawnDistance > actorSpawnDistance)
            {
                return ContinueDefensiveReturn(actor, ai);
            }

            destination = GetStoppingPoint(actor, liveTarget, collisionAdjustedRange);
            if (Distance2D(destination.X, destination.Y, spawn.Location.X, spawn.Location.Y) >
                actorSpawnDistance)
            {
                return ContinueDefensiveReturn(actor, ai);
            }

            destination = _geo.GetValidLocation(actor.Location.Location3D, destination,
                actor.getInstanceWorld());
            if (destination == actor.Location.Location3D)
            {
                return ContinueDefensiveReturn(actor, ai);
            }
            if (Distance2D(destination.X, destination.Y, spawn.Location.X, spawn.Location.Y) >
                actorSpawnDistance)
            {
                return ContinueDefensiveReturn(actor, ai);
            }
        }
        else if (intent.Constraint == NpcApproachConstraint.None)
        {
            destination = liveTarget.Location.Location3D;
        }
        else
        {
            return Reject(NpcIntentRejectionReason.InvalidIntent);
        }

        if (!_geo.CanMoveToTarget(actor.Location.Location3D, destination, actor.getInstanceWorld()))
        {
            return intent.Constraint == NpcApproachConstraint.TowardSpawnOnly
                ? ContinueDefensiveReturn(actor, ai)
                : Reject(NpcIntentRejectionReason.Blocked);
        }
        if (!actor.isRunning())
        {
            _commands.SetRunning(actor);
        }
        if (intent.Constraint == NpcApproachConstraint.TowardSpawnOnly)
        {
            // A dynamic follow could turn outward after validation. Move toward a
            // revalidated static point; the next Think may advance it again.
            _commands.StopFollow(ai);
            _commands.MoveTo(ai, destination);
        }
        // Generic movement is not enough: it may still be the old route home.
        // Only an active follow registration proves that this target is already approached.
        else if (!ReferenceEquals(actor.getTarget(), liveTarget) || !ai.isFollowing())
        {
            _commands.StartFollow(ai, liveTarget, stoppingRange, true);
        }
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ExecuteReturnHome(Attackable actor, AttackableAI ai,
        ReturnHomeIntent intent)
    {
        if (!actor.canReturnToSpawnPoint() || actor.getSpawn() is not { } spawn)
        {
            return Reject(NpcIntentRejectionReason.Policy);
        }
        if (intent.Mode is not (NpcReturnHomeMode.ResetCombat or NpcReturnHomeMode.PreserveThreat or
            NpcReturnHomeMode.TeleportReset))
        {
            return Reject(NpcIntentRejectionReason.InvalidIntent);
        }

        _commands.AbortAttack(actor);
        _commands.StopFollow(ai);
        _commands.SetWalking(actor);
        switch (intent.Mode)
        {
            case NpcReturnHomeMode.ResetCombat:
                _commands.SetTarget(actor, null);
                _commands.ClearCombatMemory(actor);
                _commands.ReturnHome(actor);
                break;
            case NpcReturnHomeMode.PreserveThreat:
                _commands.SetIntention(ai, CtrlIntention.AI_INTENTION_MOVE_TO, spawn.Location.Location3D);
                break;
            case NpcReturnHomeMode.TeleportReset:
                _commands.StopMovement(ai);
                _commands.SetTarget(actor, null);
                _commands.ClearCombatMemory(actor);
                _commands.Teleport(actor, spawn.Location, false);
                _commands.SetIntention(ai, CtrlIntention.AI_INTENTION_ACTIVE);
                break;
        }
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ContinueDefensiveReturn(Attackable actor, AttackableAI ai)
    {
        if (!actor.canReturnToSpawnPoint() || actor.getSpawn() is not { } spawn)
        {
            return Reject(NpcIntentRejectionReason.Policy);
        }

        _commands.AbortAttack(actor);
        _commands.StopFollow(ai);
        _commands.SetWalking(actor);
        _commands.SetIntention(ai, CtrlIntention.AI_INTENTION_MOVE_TO, spawn.Location.Location3D);
        return NpcIntentExecutionResult.Executed();
    }

    private static Location3D GetStoppingPoint(Attackable actor, Creature target, int stoppingRange)
    {
        double dx = target.getX() - actor.getX();
        double dy = target.getY() - actor.getY();
        double distance = double.Hypot(dx, dy);
        if (distance <= stoppingRange || distance <= 0)
        {
            return actor.Location.Location3D;
        }

        double ratio = (distance - stoppingRange) / distance;
        return new Location3D(
            actor.getX() + (int)Math.Round(dx * ratio),
            actor.getY() + (int)Math.Round(dy * ratio),
            target.getZ());
    }

    private static double Distance2D(int leftX, int leftY, int rightX, int rightY) =>
        double.Hypot((double)leftX - rightX, (double)leftY - rightY);

    private NpcIntentExecutionResult ExecuteFlee(Attackable actor, AttackableAI ai, FleeIntent intent)
    {
        if (actor.isMovementDisabled())
        {
            return Reject(NpcIntentRejectionReason.Policy);
        }

        Creature? threat = null;
        if (intent.Threat is { } threatKey)
        {
            NpcIntentExecutionResult validation = ResolveLiveTarget(actor, threatKey, false, out threat);
            if (!validation.IsExecuted)
            {
                return validation;
            }
        }
        if (threat == null)
        {
            return Reject(NpcIntentRejectionReason.TargetNotFound);
        }

        double dx = actor.getX() - threat.getX();
        double dy = actor.getY() - threat.getY();
        double length = Math.Max(1, double.Hypot(dx, dy));
        Location3D desired = new(actor.getX() + (int)(dx / length * 200),
            actor.getY() + (int)(dy / length * 200), actor.getZ());
        Location3D destination = _geo.GetValidLocation(actor.Location.Location3D, desired, actor.getInstanceWorld());
        if (destination == actor.Location.Location3D)
        {
            return Reject(NpcIntentRejectionReason.Blocked);
        }
        _commands.MoveTo(ai, destination);
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ExecuteRetreat(Attackable actor, AttackableAI ai,
        RetreatIntent intent)
    {
        if (actor.isMovementDisabled() || actor.isCastingNow())
        {
            return Reject(NpcIntentRejectionReason.Policy);
        }

        NpcIntentExecutionResult validation = ResolveLiveTarget(actor, intent.Threat, false, out Creature? threat);
        if (!validation.IsExecuted)
        {
            return validation;
        }

        // Direction: actor → away from threat.
        double dx = actor.getX() - threat!.getX();
        double dy = actor.getY() - threat.getY();
        double currentDistance = Math.Max(1, double.Hypot(dx, dy));

        // Already far enough — nothing to do (not a failure).
        if (currentDistance >= intent.Distance)
        {
            return NpcIntentExecutionResult.Executed();
        }

        // Project destination along the retreat vector to reach intent.Distance from threat.
        double ratio = intent.Distance / currentDistance;
        Location3D desired = new(
            threat.getX() + (int)(dx * ratio),
            threat.getY() + (int)(dy * ratio),
            actor.getZ());

        Location3D destination = _geo.GetValidLocation(
            actor.Location.Location3D, desired, actor.getInstanceWorld());

        if (destination == actor.Location.Location3D)
        {
            // Geo blocked all retreat in primary direction (4B5-A21).
            // Return Blocked so Brain can decide on the next Think tick.
            return Reject(NpcIntentRejectionReason.Blocked);
        }

        _commands.StopFollow(ai);
        if (!actor.isRunning())
        {
            _commands.SetRunning(actor);
        }
        _commands.MoveTo(ai, destination);
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ExecuteCast(Attackable actor, CastSkillIntent intent)
    {
        Skill? skill = actor.getKnownSkill(intent.SkillId) ?? actor.getTemplate().getSkills().get(intent.SkillId);
        if (skill == null || skill.getLevel() != intent.SkillLevel)
        {
            return Reject(NpcIntentRejectionReason.SkillNotFound);
        }
        if (actor.hasSkillReuse(skill.getReuseHashCode()) || actor.isSkillDisabled(skill))
        {
            return Reject(NpcIntentRejectionReason.Cooldown);
        }
        if (actor.getCurrentMp() < skill.getMpConsume() + skill.getMpInitialConsume())
        {
            return Reject(NpcIntentRejectionReason.InsufficientMana);
        }

        Creature? target = null;
        if (intent.Target is { } targetKey)
        {
            NpcIntentExecutionResult validation = ResolveLiveTarget(actor, targetKey, false, out target);
            if (!validation.IsExecuted)
            {
                return validation;
            }
            if (skill.getCastRange() > 0 && !actor.IsInsideRadius2D(target!, skill.getCastRange() +
                    actor.getTemplate().getCollisionRadius() + target!.getTemplate().getCollisionRadius()))
            {
                return Reject(NpcIntentRejectionReason.OutOfRange);
            }
            if (!_geo.CanSeeTarget(actor, target!))
            {
                return Reject(NpcIntentRejectionReason.Blocked);
            }
        }
        if (!SkillCaster.checkUseConditions(actor, skill))
        {
            return Reject(NpcIntentRejectionReason.SkillUnavailable);
        }

        if (target != null)
        {
            _commands.SetTarget(actor, target);
        }
        _commands.Cast(actor, skill);
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ExecuteStopCombat(Attackable actor, AttackableAI ai)
    {
        _commands.AbortAttack(actor);
        _commands.ClearCombatMemory(actor);
        _commands.SetTarget(actor, null);
        _commands.SetIntention(ai, CtrlIntention.AI_INTENTION_ACTIVE);
        return NpcIntentExecutionResult.Executed();
    }

    private NpcIntentExecutionResult ResolveLiveTarget(Attackable actor, EntityKey key, bool requireAttackable,
        out Creature? target)
    {
        WorldObject? resolved = _resolve(key.ObjectId);
        if (resolved == null)
        {
            target = null;
            return Reject(NpcIntentRejectionReason.TargetNotFound);
        }
        if (resolved is not Creature creature)
        {
            target = null;
            return Reject(NpcIntentRejectionReason.TargetInvalid);
        }
        if (key.Generation > 0 && resolved is Npc npc && npc.getSpawnGeneration() != key.Generation)
        {
            target = null;
            return Reject(NpcIntentRejectionReason.TargetInvalid);
        }
        if (creature.isAlikeDead())
        {
            target = null;
            return Reject(NpcIntentRejectionReason.TargetDead);
        }
        if (!creature.isSpawned() || !creature.isTargetable())
        {
            target = null;
            return Reject(NpcIntentRejectionReason.TargetInvalid);
        }
        if (actor.getInstanceId() != creature.getInstanceId())
        {
            target = null;
            return Reject(NpcIntentRejectionReason.InstanceMismatch);
        }
        // Retaliation and faction assistance are authoritative hostility too. Guards, in
        // particular, may legally attack a player who damaged a guard even before that
        // player becomes generically auto-attackable to every NPC.
        if (requireAttackable && !creature.isAutoAttackable(actor) && actor.getHating(creature) <= 0)
        {
            target = null;
            return Reject(NpcIntentRejectionReason.TargetInvalid);
        }

        target = creature;
        return NpcIntentExecutionResult.Executed();
    }

    private static NpcIntentType GetIntentType(NpcIntent intent) => intent switch
    {
        AcquireTargetIntent => NpcIntentType.AcquireTarget,
        ClearTargetIntent => NpcIntentType.ClearTarget,
        BasicAttackIntent => NpcIntentType.BasicAttack,
        ApproachTargetIntent => NpcIntentType.ApproachTarget,
        ReturnHomeIntent => NpcIntentType.ReturnHome,
        FleeIntent => NpcIntentType.Flee,
        RetreatIntent => NpcIntentType.Retreat,
        CastSkillIntent => NpcIntentType.CastSkill,
        StopCombatIntent => NpcIntentType.StopCombat,
        _ => 0
    };

    private static NpcIntentExecutionResult Reject(NpcIntentRejectionReason reason) =>
        NpcIntentExecutionResult.Rejected(reason);
}
