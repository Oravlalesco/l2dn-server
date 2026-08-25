using FluentAssertions;
using L2Dn.GameServer.AI;
using L2Dn.GameServer.AI.Runtime;
using L2Dn.GameServer.AI.Scheduling;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Actor.Instances;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.InstanceZones;
using L2Dn.GameServer.Model.Items.Instances;
using L2Dn.GameServer.Model.Skills;
using L2Dn.Geometry;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.GameServer.Model.Tests;

public class NpcIntentGatewayTests
{
    private static int _nextTemplateId = 9_200_000;

    [Fact]
    public void Generation_mismatch_is_rejected_before_execution()
    {
        Attackable actor = CreateActor();
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor);
        BasicAttackIntent intent = AttackIntent(actor, new EntityKey(999, 0, EntityKind.Player),
            actor.getSpawnGeneration() - 1);

        NpcIntentExecutionResult result = gateway.Execute(intent);

        result.RejectionReason.Should().Be(NpcIntentRejectionReason.GenerationMismatch);
        commands.AutoAttackTarget.Should().BeNull();
    }

    [Fact]
    public void Stale_revision_is_live_revalidated_and_can_execute()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, target, currentRevision: 99);

        NpcIntentExecutionResult result = gateway.Execute(AttackIntent(actor, Key(target), revision: 1));

        result.IsExecuted.Should().BeTrue();
        commands.AutoAttackTarget.Should().BeSameAs(target);
    }

    [Fact]
    public void Existing_hate_authorizes_guard_style_retaliation_against_non_auto_attackable_target()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateNonAutoAttackableTarget();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, target);

        NpcIntentExecutionResult result = gateway.Execute(AttackIntent(actor, Key(target)));

        result.IsExecuted.Should().BeTrue();
        commands.AutoAttackTarget.Should().BeSameAs(target);
    }

    [Fact]
    public void Acquiring_attacker_cancels_previous_return_home_movement()
    {
        Attackable actor = CreateActor();
        Attackable attacker = CreateNonAutoAttackableTarget();
        actor.addDamageHate(attacker, 10, 100);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, attacker);
        AcquireTargetIntent intent = new(Envelope(actor, NpcIntentType.AcquireTarget), Key(attacker));

        NpcIntentExecutionResult result = gateway.Execute(intent);

        result.IsExecuted.Should().BeTrue();
        commands.StopFollowCalls.Should().Be(1);
        commands.StopMovementCalls.Should().Be(1);
        commands.Intention.Should().Be(CtrlIntention.AI_INTENTION_ATTACK);
        commands.IntentionTarget.Should().BeSameAs(attacker);
    }

    [Fact]
    public void Fresh_acquisition_outside_authoritative_aggro_range_is_rejected()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateAutoAttackableTarget();
        target.setXYZ(actor.getX() + 501, actor.getY(), actor.getZ());
        AcquireTargetIntent intent = new(Envelope(actor, NpcIntentType.AcquireTarget), Key(target));
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor, target);

        gateway.Execute(intent).RejectionReason.Should().Be(NpcIntentRejectionReason.OutOfRange);
    }

    [Fact]
    public void Fresh_acquisition_without_line_of_sight_is_rejected()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateAutoAttackableTarget();
        target.setXYZ(actor.getX() + 100, actor.getY(), actor.getZ());
        AcquireTargetIntent intent = new(Envelope(actor, NpcIntentType.AcquireTarget), Key(target));
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), false, actor, target);

        gateway.Execute(intent).RejectionReason.Should().Be(NpcIntentRejectionReason.Blocked);
    }

    [Fact]
    public void Target_dying_between_decision_and_gateway_is_rejected_without_exception()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        BasicAttackIntent intent = AttackIntent(actor, Key(target));
        target.setDead(true);
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor, target);

        NpcIntentExecutionResult result = gateway.Execute(intent);

        result.RejectionReason.Should().Be(NpcIntentRejectionReason.TargetDead);
    }

    [Fact]
    public void Target_disappearing_between_decision_and_gateway_is_rejected()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        BasicAttackIntent intent = AttackIntent(actor, Key(target));
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor);

        gateway.Execute(intent).RejectionReason.Should().Be(NpcIntentRejectionReason.TargetNotFound);
    }

    [Fact]
    public void Actor_becoming_disabled_before_attack_is_rejected()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        actor.disableAllSkills();
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor, target);

        gateway.Execute(AttackIntent(actor, Key(target))).RejectionReason
            .Should().Be(NpcIntentRejectionReason.Policy);
    }

    [Fact]
    public void Generic_approach_requests_one_action_ready_wakeup_when_follow_range_is_reached()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        target.setXYZ(500, 0, 0);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, target);
        ApproachTargetIntent intent = new(Envelope(actor, NpcIntentType.ApproachTarget), Key(target), 40);

        gateway.Execute(intent).IsExecuted.Should().BeTrue();

        commands.StartFollowCalls.Should().Be(1);
        commands.WakeWhenFollowRangeReached.Should().BeTrue();
    }

    [Fact]
    public void Movement_blocked_after_decision_is_rejected_by_authoritative_geo()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        target.setXYZ(500, 0, 0);
        NpcIntentEnvelope envelope = Envelope(actor, NpcIntentType.ApproachTarget);
        ApproachTargetIntent intent = new(envelope, Key(target), 40);
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), false, actor, target);

        gateway.Execute(intent).RejectionReason.Should().Be(NpcIntentRejectionReason.Blocked);
    }

    [Fact]
    public void Homeward_only_approach_uses_static_movement_toward_spawn()
    {
        Attackable actor = CreateActorWithSpawn(0, 0, 0);
        Attackable target = CreateActor();
        actor.setXYZ(1_600, 0, 0);
        target.setXYZ(1_500, 0, 0);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, target);
        ApproachTargetIntent intent = new(Envelope(actor, NpcIntentType.ApproachTarget), Key(target), 40,
            NpcApproachConstraint.TowardSpawnOnly);

        NpcIntentExecutionResult result = gateway.Execute(intent);

        result.IsExecuted.Should().BeTrue();
        commands.StartFollowCalls.Should().Be(0);
        commands.StopFollowCalls.Should().Be(1);
        commands.MoveDestination.Should().NotBeNull();
        commands.MoveDestination!.Value.X.Should().BeLessThan(actor.getX());
    }

    [Fact]
    public void Homeward_only_approach_falls_back_to_return_when_target_moved_outward()
    {
        Attackable actor = CreateActorWithSpawn(0, 0, 0);
        Attackable target = CreateActor();
        actor.setXYZ(1_600, 0, 0);
        target.setXYZ(1_700, 0, 0);
        actor.addDamageHate(target, 100, 100);
        actor.setTarget(target);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, target);
        ApproachTargetIntent intent = new(Envelope(actor, NpcIntentType.ApproachTarget), Key(target), 40,
            NpcApproachConstraint.TowardSpawnOnly);

        gateway.Execute(intent).IsExecuted.Should().BeTrue();
        commands.MoveDestination.Should().BeNull();
        commands.StartFollowCalls.Should().Be(0);
        commands.Intention.Should().Be(CtrlIntention.AI_INTENTION_MOVE_TO);
        actor.getTarget().Should().BeSameAs(target);
        actor.getHating(target).Should().BePositive();
    }

    [Fact]
    public void Homeward_only_approach_falls_back_to_return_when_geo_redirects_outward()
    {
        Attackable actor = CreateActorWithSpawn(0, 0, 0);
        Attackable target = CreateActor();
        actor.setXYZ(1_600, 0, 0);
        target.setXYZ(1_500, 0, 0);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = new(
            id => new[] { actor, target }.FirstOrDefault(item => item.ObjectId == id), _ => 1,
            new RedirectingGeo(new Location3D(1_700, 0, 0)), commands);
        ApproachTargetIntent intent = new(Envelope(actor, NpcIntentType.ApproachTarget), Key(target), 40,
            NpcApproachConstraint.TowardSpawnOnly);

        gateway.Execute(intent).IsExecuted.Should().BeTrue();
        commands.MoveDestination.Should().BeNull();
        commands.Intention.Should().Be(CtrlIntention.AI_INTENTION_MOVE_TO);
    }

    [Fact]
    public void Defensive_retarget_preserves_the_current_return_movement()
    {
        Attackable actor = CreateActorWithSpawn(0, 0, 0);
        Attackable attacker = CreateActor();
        actor.setXYZ(1_600, 0, 0);
        attacker.setXYZ(1_580, 0, 0);
        actor.addDamageHate(attacker, 100, 100);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, attacker);

        NpcIntentExecutionResult result = gateway.Execute(new AcquireTargetIntent(
            Envelope(actor, NpcIntentType.AcquireTarget), Key(attacker),
            NpcTargetAcquisitionMode.PreserveMovement));

        result.IsExecuted.Should().BeTrue();
        actor.getTarget().Should().BeSameAs(attacker);
        commands.StopMovementCalls.Should().Be(0);
        commands.Intention.Should().BeNull();
    }

    [Fact]
    public void Defensive_return_preserves_target_and_threat_while_moving_home()
    {
        Attackable actor = CreateActorWithSpawn(0, 0, 0);
        Attackable attacker = CreateActor();
        actor.setXYZ(1_600, 0, 0);
        attacker.setXYZ(1_700, 0, 0);
        actor.addDamageHate(attacker, 100, 100);
        actor.setTarget(attacker);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, attacker);
        ReturnHomeIntent intent = new(Envelope(actor, NpcIntentType.ReturnHome),
            NpcReturnHomeMode.PreserveThreat);

        NpcIntentExecutionResult result = gateway.Execute(intent);

        result.IsExecuted.Should().BeTrue();
        actor.getTarget().Should().BeSameAs(attacker);
        actor.getHating(attacker).Should().BePositive();
        commands.ClearCombatMemoryCalls.Should().Be(0);
        commands.ReturnHomeCalls.Should().Be(0);
        commands.Intention.Should().Be(CtrlIntention.AI_INTENTION_MOVE_TO);
    }

    [Fact]
    public void Reset_return_clears_target_and_combat_memory()
    {
        Attackable actor = CreateActorWithSpawn(0, 0, 0);
        Attackable attacker = CreateActor();
        actor.addDamageHate(attacker, 100, 100);
        actor.setTarget(attacker);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, attacker);

        gateway.Execute(new ReturnHomeIntent(Envelope(actor, NpcIntentType.ReturnHome)))
            .IsExecuted.Should().BeTrue();

        actor.getTarget().Should().BeNull();
        commands.ClearCombatMemoryCalls.Should().Be(1);
        commands.ReturnHomeCalls.Should().Be(1);
    }

    [Fact]
    public void Emergency_return_teleports_home_and_forgets_combat()
    {
        Attackable actor = CreateActorWithSpawn(100, 200, 300);
        Attackable attacker = CreateActor();
        actor.setXYZ(2_000, 0, 0);
        actor.addDamageHate(attacker, 100, 100);
        actor.setTarget(attacker);
        RecordingCommands commands = new();
        NpcIntentGateway gateway = CreateGateway(commands, actor, attacker);

        NpcIntentExecutionResult result = gateway.Execute(new ReturnHomeIntent(
            Envelope(actor, NpcIntentType.ReturnHome), NpcReturnHomeMode.TeleportReset));

        result.IsExecuted.Should().BeTrue();
        commands.TeleportCalls.Should().Be(1);
        commands.TeleportDestination.Should().Be(new Location(100, 200, 300, 0));
        commands.StopMovementCalls.Should().Be(1);
        commands.ClearCombatMemoryCalls.Should().Be(1);
        commands.Intention.Should().Be(CtrlIntention.AI_INTENTION_ACTIVE);
        actor.getTarget().Should().BeNull();
        actor.getHating(attacker).Should().Be(0);
    }

    [Fact]
    public void Skill_entering_cooldown_after_decision_is_rejected()
    {
        Attackable actor = CreateActor();
        Skill skill = CreateSkill(7001, 1);
        actor.addSkill(skill);
        actor.addTimeStamp(skill, TimeSpan.FromSeconds(30));
        CastSkillIntent intent = new(Envelope(actor, NpcIntentType.CastSkill), skill.getId(), skill.getLevel(), null);
        NpcIntentGateway gateway = CreateGateway(new RecordingCommands(), actor);

        gateway.Execute(intent).RejectionReason.Should().Be(NpcIntentRejectionReason.Cooldown);
    }

    [Fact]
    public void S8_actor_dying_after_strategy_decision_is_rejected_by_gateway()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        BasicAttackIntent strategyIntent = AttackIntent(actor, Key(target));
        actor.setDead(true);

        NpcIntentExecutionResult result = CreateGateway(new RecordingCommands(), actor, target)
            .Execute(strategyIntent);

        result.RejectionReason.Should().Be(NpcIntentRejectionReason.ActorDead);
    }

    [Fact]
    public void S9_generation_changing_after_strategy_decision_is_rejected_by_gateway()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        actor.addDamageHate(target, 0, 10);
        actor.setTarget(target);
        BasicAttackIntent strategyIntent = AttackIntent(actor, Key(target));
        actor.beginRespawnLifecycle();

        NpcIntentExecutionResult result = CreateGateway(new RecordingCommands(), actor, target)
            .Execute(strategyIntent);

        result.RejectionReason.Should().Be(NpcIntentRejectionReason.GenerationMismatch);
    }

    [Fact]
    public void Strategy_skill_preference_cannot_bypass_live_mana_validation()
    {
        Attackable actor = CreateActor();
        Skill skill = CreateSkill(7002, 1, mpConsume: 50);
        actor.addSkill(skill);
        actor.setCurrentMp(0);
        CastSkillIntent strategyIntent = new(Envelope(actor, NpcIntentType.CastSkill),
            skill.getId(), skill.getLevel(), null);

        CreateGateway(new RecordingCommands(), actor).Execute(strategyIntent).RejectionReason
            .Should().Be(NpcIntentRejectionReason.InsufficientMana);
    }

    [Fact]
    public void Strategy_skill_preference_cannot_bypass_live_range_validation()
    {
        Attackable actor = CreateActor();
        Attackable target = CreateActor();
        target.setXYZ(500, 0, 0);
        Skill skill = CreateSkill(7003, 1, castRange: 100);
        actor.addSkill(skill);
        CastSkillIntent strategyIntent = new(Envelope(actor, NpcIntentType.CastSkill),
            skill.getId(), skill.getLevel(), Key(target));

        CreateGateway(new RecordingCommands(), actor, target).Execute(strategyIntent).RejectionReason
            .Should().Be(NpcIntentRejectionReason.OutOfRange);
    }

    [Fact]
    public void Brain_mode_parses_independently_from_reactive_scheduler_mode()
    {
        NpcBrainOptions defaults = NpcBrainOptions.FromEnvironment(
            name => name == "NPC_BRAIN_MODE" ? "shadow" : null);
        defaults.Mode.Should().Be(NpcBrainMode.Shadow);
        defaults.ReturnDefense.Enabled.Should().BeTrue();
        defaults.ReturnDefense.TimeoutWorldTicks.Should().Be(1200);
        defaults.ReturnDefense.LeashGraceWorldTicks.Should().Be(200);
        defaults.ReturnDefense.MaxLeashExcursions.Should().Be(3);
        defaults.ReturnDefense.HardLeashExtension.Should().Be(500);
        NpcBrainOptions.FromEnvironment(name => name == "NPC_BRAIN_MODE" ? "INTENT" : null).Mode
            .Should().Be(NpcBrainMode.Intent);
        NpcBrainOptions.FromEnvironment(_ => "invalid").Mode.Should().Be(NpcBrainMode.Legacy);

        NpcBrainOptions configured = NpcBrainOptions.FromEnvironment(name => name switch
        {
            "NPC_BRAIN_MODE" => "intent",
            "NPC_RETURN_DEFENSE_ENABLED" => "false",
            "NPC_RETURN_DEFENSE_TIMEOUT_MS" => "2500",
            "NPC_LEASH_GRACE_MS" => "3500",
            "NPC_LEASH_MAX_EXCURSIONS" => "2",
            "NPC_LEASH_HARD_EXTENSION" => "750",
            _ => null
        });
        configured.ReturnDefense.Enabled.Should().BeFalse();
        configured.ReturnDefense.TimeoutWorldTicks.Should().Be(25);
        configured.ReturnDefense.LeashGraceWorldTicks.Should().Be(35);
        configured.ReturnDefense.MaxLeashExcursions.Should().Be(2);
        configured.ReturnDefense.HardLeashExtension.Should().Be(750);
    }

    [Fact]
    public void Strategy_configuration_builds_an_immutable_bounded_template_registry_at_startup()
    {
        List<string> warnings = [];
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(NpcBrainMode.Intent,
            NpcReactiveSchedulerMode.Enabled, name => name switch
            {
                "NPC_STRATEGY_MODE" => "shadow",
                "NPC_STRATEGY_TEMPLATE_PROFILES" =>
                    " 20003:Balanced, 20004:AGGRESSIVE_PRESSURE, 21101:ranged-control," +
                    "20292:survival, 20004:survival, 30000:agressive_pressure, broken ",
                _ => null
            }, warnings.Add);

        options.EffectiveMode.Should().Be(NpcStrategyMode.Shadow);
        options.Registry.Count.Should().Be(4);
        options.Registry.TryResolve(20003, out NpcStrategyProfile balanced, out _).Should().BeTrue();
        balanced.Should().BeSameAs(NpcStrategyProfileResolver.Balanced);
        options.Registry.TryResolve(20004, out NpcStrategyProfile duplicate, out _).Should().BeTrue();
        duplicate.Should().BeSameAs(NpcStrategyProfileResolver.Survival);
        options.Registry.TryResolve(30000, out _, out _).Should().BeFalse();
        warnings.Should().Contain(message => message.Contains("last entry wins", StringComparison.Ordinal));
        warnings.Should().Contain(message => message.Contains("unknown", StringComparison.Ordinal));
        warnings.Should().Contain(message => message.Contains("malformed", StringComparison.Ordinal));
    }

    [Fact]
    public void Talking_island_rollout_adds_every_area_template_without_replacing_the_laboratory_set()
    {
        const string Configuration =
            "20003:balanced,20004:aggressive_pressure,21101:ranged_control,20292:survival," +
            "20016:balanced,20120:balanced,20121:balanced,20432:balanced,20442:balanced,20481:balanced,20544:balanced," +
            "20093:aggressive_pressure,20096:aggressive_pressure,20098:aggressive_pressure," +
            "20103:aggressive_pressure,20106:aggressive_pressure,20108:aggressive_pressure," +
            "20130:aggressive_pressure,20131:aggressive_pressure,20132:aggressive_pressure," +
            "20326:aggressive_pressure,20342:aggressive_pressure,20343:aggressive_pressure," +
            "20006:ranged_control,20101:ranged_control," +
            "20110:ranged_control,20113:ranged_control,20115:ranged_control";
        NpcStrategyTemplateRegistry registry = NpcStrategyTemplateRegistry.Parse(Configuration);

        registry.Count.Should().Be(28);
        AssertProfiles(registry, NpcStrategyArchetype.Balanced,
            20003, 20016, 20120, 20121, 20432, 20442, 20481, 20544);
        AssertProfiles(registry, NpcStrategyArchetype.AggressivePressure,
            20004, 20093, 20096, 20098, 20103, 20106, 20108, 20130, 20131, 20132,
            20326, 20342, 20343);
        AssertProfiles(registry, NpcStrategyArchetype.RangedControl,
            21101, 20006, 20101, 20110, 20113, 20115);
        AssertProfiles(registry, NpcStrategyArchetype.Survival, 20292);
    }

    [Theory]
    [InlineData(NpcBrainMode.Legacy, NpcReactiveSchedulerMode.Enabled)]
    [InlineData(NpcBrainMode.Shadow, NpcReactiveSchedulerMode.Enabled)]
    [InlineData(NpcBrainMode.Intent, NpcReactiveSchedulerMode.Disabled)]
    [InlineData(NpcBrainMode.Intent, NpcReactiveSchedulerMode.Shadow)]
    public void Unsupported_strategy_mode_combinations_disable_only_strategy(
        NpcBrainMode brainMode, NpcReactiveSchedulerMode reactiveMode)
    {
        bool registryRead = false;
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(brainMode, reactiveMode, name =>
        {
            if (name == "NPC_STRATEGY_TEMPLATE_PROFILES")
            {
                registryRead = true;
            }
            return name == "NPC_STRATEGY_MODE" ? "enabled" : "20003:survival";
        });

        options.ConfiguredMode.Should().Be(NpcStrategyMode.Enabled);
        options.EffectiveMode.Should().Be(NpcStrategyMode.Disabled);
        options.Registry.Count.Should().Be(0);
        registryRead.Should().BeFalse();
    }

    [Fact]
    public void Shadow_comparison_is_semantic_across_revision_metadata()
    {
        Attackable actor = CreateActor();
        EntityKey target = new(8127, 0, EntityKind.Player);
        AcquireTargetIntent brain = new(Envelope(actor, NpcIntentType.AcquireTarget, revision: 1), target);
        AcquireTargetIntent legacy = new(Envelope(actor, NpcIntentType.AcquireTarget, revision: 9), target);

        ShadowNpcThinkExecutor.Compare([brain], [legacy]).Should().Be(NpcIntentComparisonKind.SemanticMatch);
    }

    /// <summary>
    /// 4B5-A21: When every direction is geo-blocked, ExecuteRetreat returns Rejected(Blocked).
    /// The actor does not move and no exception is thrown ("no freeze" invariant).
    /// TestGeo(false) makes GetValidLocation return source unchanged, simulating full blockage.
    /// </summary>
    [Fact]
    public void Retreat_GeoBlocked_returns_Blocked()
    {
        Attackable actor = CreateActor();
        Attackable threat = CreateActor();
        actor.addDamageHate(threat, 0, 10);
        // Place threat close to actor so retreat distance is not already satisfied.
        threat.setXYZ(actor.getX() + 50, actor.getY(), actor.getZ());

        RecordingCommands commands = new();
        // TestGeo(false): GetValidLocation returns source = actor's position unchanged.
        NpcIntentGateway gateway = CreateGateway(commands, false, actor, threat);

        RetreatIntent intent = new(
            Envelope(actor, NpcIntentType.Retreat),
            Key(threat),
            distance: 300);

        NpcIntentExecutionResult result = gateway.Execute(intent);

        result.Status.Should().Be(NpcIntentExecutionStatus.Rejected,
            because: "geo is fully blocked, retreat destination equals current position");
        result.RejectionReason.Should().Be(NpcIntentRejectionReason.Blocked,
            because: "4B5-A21: blocked geo must return Blocked, never freeze or throw");
        commands.MoveDestination.Should().BeNull(
            because: "actor must not have moved when retreat was blocked");
    }

    private static NpcIntentGateway CreateGateway(RecordingCommands commands, params Attackable[] actors) =>
        CreateGateway(commands, true, actors);

    private static NpcIntentGateway CreateGateway(RecordingCommands commands, Attackable actor,
        Attackable target, long currentRevision) =>
        new(id => new[] { actor, target }.FirstOrDefault(item => item.ObjectId == id), _ => currentRevision,
            new TestGeo(true), commands);

    private static NpcIntentGateway CreateGateway(RecordingCommands commands, bool geoAllowed,
        params Attackable[] actors) =>
        new(id => actors.FirstOrDefault(item => item.ObjectId == id), _ => 1,
            new TestGeo(geoAllowed), commands);

    private static void AssertProfiles(NpcStrategyTemplateRegistry registry,
        NpcStrategyArchetype expected, params int[] templateIds)
    {
        foreach (int templateId in templateIds)
        {
            registry.TryResolve(templateId, out NpcStrategyProfile profile, out bool fallback)
                .Should().BeTrue();
            fallback.Should().BeFalse();
            profile.Archetype.Should().Be(expected);
        }
    }

    private static BasicAttackIntent AttackIntent(Attackable actor, EntityKey target, int? generation = null,
        long revision = 1) =>
        new(new NpcIntentEnvelope(1, new NpcKey(actor.ObjectId, generation ?? actor.getSpawnGeneration()),
            revision, 1, NpcIntentType.BasicAttack), target);

    private static NpcIntentEnvelope Envelope(Attackable actor, NpcIntentType type, long revision = 1) =>
        new(1, new NpcKey(actor.ObjectId, actor.getSpawnGeneration()), revision, 1, type);

    private static EntityKey Key(Attackable actor) =>
        new(actor.ObjectId, actor.getSpawnGeneration(), EntityKind.Npc);

    private static Attackable CreateActor()
    {
        return InitializeActor(new Monster(CreateNpcTemplate()));
    }

    private static Attackable CreateActorWithSpawn(int x, int y, int z)
    {
        Attackable actor = CreateActor();
        actor.setSpawn(new Spawn(actor.getTemplate()) { Location = new Location(x, y, z, 0) });
        return actor;
    }

    private static Attackable CreateNonAutoAttackableTarget()
    {
        return InitializeActor(new NonAutoAttackableAttackable(CreateNpcTemplate()));
    }

    private static Attackable CreateAutoAttackableTarget()
    {
        return InitializeActor(new AutoAttackableAttackable(CreateNpcTemplate()));
    }

    private static T InitializeActor<T>(T actor) where T: Attackable
    {
        actor.beginRespawnLifecycle();
        actor.onRespawn();
        actor.completeRespawnLifecycle();
        actor.setSpawned(true);
        _ = actor.getAI();
        return actor;
    }

    private static NpcTemplate CreateNpcTemplate()
    {
        StatSet set = new();
        set.set("id", Interlocked.Increment(ref _nextTemplateId));
        set.set("type", "Monster");
        set.set("name", "Intent Test NPC");
        set.set("baseHpMax", 100d);
        set.set("baseMpMax", 100d);
        set.set("aggroRange", 500);
        return new NpcTemplate(set);
    }

    private static Skill CreateSkill(int id, int level, int mpConsume = 0, int castRange = -1)
    {
        StatSet set = new();
        set.set(".id", id);
        set.set(".level", level);
        set.set(".name", "Intent Test Skill");
        set.set("operateType", SkillOperateType.A1);
        set.set("mpConsume", mpConsume);
        set.set("castRange", castRange);
        return new Skill(set);
    }

    private sealed class TestGeo(bool allowed): INpcGeoQuery
    {
        public bool CanSeeTarget(WorldObject source, WorldObject target) => allowed;
        public bool CanMoveToTarget(Location3D source, Location3D target, Instance? instance) => allowed;
        public Location3D GetValidLocation(Location3D source, Location3D target, Instance? instance) =>
            allowed ? target : source;
    }

    private sealed class RedirectingGeo(Location3D destination): INpcGeoQuery
    {
        public bool CanSeeTarget(WorldObject source, WorldObject target) => true;
        public bool CanMoveToTarget(Location3D source, Location3D target, Instance? instance) => true;
        public Location3D GetValidLocation(Location3D source, Location3D target, Instance? instance) => destination;
    }

    private sealed class NonAutoAttackableAttackable(NpcTemplate template): Attackable(template)
    {
        public override bool isAutoAttackable(Creature attacker) => false;
    }

    private sealed class AutoAttackableAttackable(NpcTemplate template): Attackable(template)
    {
        public override bool isAutoAttackable(Creature attacker) => true;
    }

    private sealed class RecordingCommands: ILegacyNpcCommandExecutor
    {
        public Creature? AutoAttackTarget { get; private set; }
        public int StopFollowCalls { get; private set; }
        public int StartFollowCalls { get; private set; }
        public bool WakeWhenFollowRangeReached { get; private set; }
        public int StopMovementCalls { get; private set; }
        public int ClearCombatMemoryCalls { get; private set; }
        public int ReturnHomeCalls { get; private set; }
        public int TeleportCalls { get; private set; }
        public Location? TeleportDestination { get; private set; }
        public Location3D? MoveDestination { get; private set; }
        public CtrlIntention? Intention { get; private set; }
        public object? IntentionTarget { get; private set; }
        public void AutoAttack(Creature actor, Creature target) => AutoAttackTarget = target;
        public void SetIntention(AbstractAI ai, CtrlIntention intention, object? argument = null)
        {
            Intention = intention;
            IntentionTarget = argument;
        }
        public void MoveTo(AbstractAI ai, Location3D destination) => MoveDestination = destination;
        public void StartFollow(AbstractAI ai, Creature target, int range = -1,
            bool wakeWhenInRange = false)
        {
            StartFollowCalls++;
            WakeWhenFollowRangeReached = wakeWhenInRange;
        }
        public void StopFollow(AbstractAI ai) => StopFollowCalls++;
        public void StopMovement(AbstractAI ai) => StopMovementCalls++;
        public void SetTarget(Creature actor, WorldObject? target) => actor.setTarget(target);
        public void SetRunning(Creature actor) => actor.setRunning();
        public void SetWalking(Creature actor) => actor.setWalking();
        public void ReturnHome(Attackable npc) => ReturnHomeCalls++;
        public void RestoreFullHealth(Creature actor) { }
        public void Teleport(Creature actor, Location destination, bool randomOffset)
        {
            TeleportCalls++;
            TeleportDestination = destination;
        }
        public void AbortAttack(Creature actor) { }
        public void Cast(Creature actor, Skill skill, Item? item = null, bool forceUse = false, bool dontMove = false) { }
        public void AddThreat(Attackable npc, Creature target, long damage, long hate) =>
            npc.addDamageHate(target, damage, hate);
        public void StopHating(Attackable npc, Creature? target) => npc.stopHating(target);
        public void ClearCombatMemory(Attackable npc)
        {
            ClearCombatMemoryCalls++;
            npc.clearAggroList();
        }
        public void PickUpDroppedItem(Attackable npc, Item item) { }
    }
}
