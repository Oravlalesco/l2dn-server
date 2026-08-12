using System.Collections.Immutable;
using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

public class NpcBrainTests
{
    private static readonly NpcKey Actor = new(8172, 4);
    private static readonly EntityKey Player = new(9182, 0, EntityKind.Player);

    [Fact]
    public void Brain_assembly_references_only_contracts_from_L2Dn()
    {
        string[] references = typeof(NpcBrainCoordinator).Assembly.GetReferencedAssemblies()
            .Select(static assembly => assembly.Name ?? string.Empty)
            .Where(static name => name.StartsWith("L2Dn.", StringComparison.Ordinal))
            .ToArray();

        references.Should().Equal("L2Dn.Npc.Contracts");
    }

    [Fact]
    public void Dead_actor_produces_no_intent()
    {
        NpcPerceptionSnapshot perception = CreatePerception(physicalFlags: NpcPhysicalFlags.Spawned);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Aggressive_actor_acquires_nearest_visible_hostile()
    {
        VisibleEntity far = Hostile(new EntityKey(9991, 0, EntityKind.Player), 300, 0);
        VisibleEntity near = Hostile(Player, 90, 1);
        NpcPerceptionSnapshot perception = CreatePerception(visible: [far, near]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Layer.Should().Be(NpcBrainLayer.Reflex);
        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Aggressive_actor_does_not_acquire_target_outside_vertical_aggro_range()
    {
        VisibleEntity verticallyDistant = Hostile(Player, 0) with
        {
            Position = new NpcPosition(0, 0, 600, 0)
        };
        NpcPerceptionSnapshot perception = CreatePerception(visible: [verticallyDistant]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Aggressive_actor_does_not_acquire_visible_entity_without_attack_authority()
    {
        VisibleEntity neutral = Hostile(Player, 90) with
        {
            Relations = EntityRelationFlags.Player | EntityRelationFlags.Playable |
                EntityRelationFlags.SameInstance
        };
        NpcPerceptionSnapshot perception = CreatePerception(visible: [neutral]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Aggressive_monster_does_not_acquire_new_target_in_protected_peace_zone()
    {
        VisibleEntity protectedPlayer = Hostile(Player, 90) with
        {
            State = EntityStateFlags.Alive | EntityStateFlags.Spawned |
                EntityStateFlags.PeaceZone | EntityStateFlags.NoPvpZone
        };
        NpcPerceptionSnapshot perception = CreatePerception(visible: [protectedPlayer]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Guard_acquires_authorized_target_inside_protected_peace_zone()
    {
        VisibleEntity criminal = Hostile(Player, 90) with
        {
            State = EntityStateFlags.Alive | EntityStateFlags.Spawned |
                EntityStateFlags.PeaceZone | EntityStateFlags.NoPvpZone
        };
        NpcCapabilities capabilities = NpcCapabilities.CanMove | NpcCapabilities.CanAttack |
            NpcCapabilities.Guard | NpcCapabilities.CanAcquireInPeaceZone;
        NpcPerceptionSnapshot perception = CreatePerception(visible: [criminal], capabilities: capabilities);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Passive_actor_does_not_acquire_visible_player_without_threat()
    {
        NpcPerceptionSnapshot perception = CreatePerception(visible: [Hostile(Player, 90)],
            capabilities: NpcCapabilities.CanMove | NpcCapabilities.CanAttack);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Threat_is_acquired_even_by_passive_actor()
    {
        NpcPerceptionSnapshot perception = CreatePerception(threats:
            [new ThreatEntry(Player, 10, 2, 100, true, true)],
            capabilities: NpcCapabilities.CanMove | NpcCapabilities.CanAttack);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.Attacked));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Higher_hate_visible_threat_replaces_current_target()
    {
        EntityKey challenger = new(9277, 0, EntityKind.Player);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30), Hostile(challenger, 40, 1)],
            threats:
            [
                new ThreatEntry(Player, 10, 100, 30, true, true),
                new ThreatEntry(challenger, 200, 200, 40, true, true)
            ]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.ThreatChanged));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(challenger);
    }

    [Fact]
    public void Lower_hate_visible_threat_does_not_replace_current_target()
    {
        EntityKey challenger = new(9277, 0, EntityKind.Player);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30), Hostile(challenger, 40, 1)],
            threats:
            [
                new ThreatEntry(Player, 200, 200, 30, true, true),
                new ThreatEntry(challenger, 10, 10, 40, true, true)
            ]);

        NpcIntent intent = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.ThreatChanged)).Intents.Single();

        intent.Should().BeOfType<BasicAttackIntent>().Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Threat_that_left_world_visibility_is_not_reacquired()
    {
        NpcPerceptionSnapshot perception = CreatePerception(threats:
            [new ThreatEntry(Player, 10, 2, 2_000, false, true)],
            capabilities: NpcCapabilities.CanMove | NpcCapabilities.CanAttack);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.TargetLost));

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Dead_current_target_is_cleared()
    {
        VisibleEntity dead = Hostile(Player, 30) with { State = EntityStateFlags.Spawned | EntityStateFlags.AlikeDead };
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [dead]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.TargetDied));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ClearTargetIntent>()
            .Which.ExpectedTarget.Should().Be(Player);
    }

    [Fact]
    public void Valid_distant_target_is_approached()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [Hostile(Player, 300)]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Layer.Should().Be(NpcBrainLayer.Tactical);
        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ApproachTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Valid_target_in_range_is_attacked()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [Hostile(Player, 30)]);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<BasicAttackIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Target_inside_collision_adjusted_physical_reach_is_attacked_instead_of_stalling()
    {
        // Physical range is 40, actor radius is 8, and target radius is 10.
        // The authoritative Gateway permits an attack at 58, so the Brain must
        // not keep emitting no-op approach intents inside that same envelope.
        VisibleEntity target = Hostile(Player, 55) with { CollisionRadius = 10 };
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [target]);

        NpcIntent intent = new NpcBrainCoordinator().Decide(perception, Context()).Intents.Single();

        intent.Should().BeOfType<BasicAttackIntent>().Which.Target.Should().Be(Player);
    }

    [Theory]
    [InlineData(LegacyNpcAiType.Fighter, NpcStrategyArchetype.AggressivePressure)]
    [InlineData(LegacyNpcAiType.Mage, NpcStrategyArchetype.RangedControl)]
    [InlineData(LegacyNpcAiType.Healer, NpcStrategyArchetype.Survival)]
    [InlineData(LegacyNpcAiType.Balanced, NpcStrategyArchetype.Balanced)]
    public void Strategy_profile_is_resolved_deterministically_from_immutable_identity(
        LegacyNpcAiType aiType, NpcStrategyArchetype expected)
    {
        NpcPerceptionSnapshot perception = CreatePerception(legacyAiType: aiType);
        NpcIntelligenceProfile intelligence = NpcIntelligenceProfileResolver.Instance
            .Resolve(perception.State.Identity);

        NpcStrategyProfile first = NpcStrategyProfileResolver.Instance
            .Resolve(perception.State.Identity, intelligence);
        NpcStrategyProfile second = NpcStrategyProfileResolver.Instance
            .Resolve(perception.State.Identity, intelligence);

        first.Archetype.Should().Be(expected);
        second.Should().BeSameAs(first);
    }

    [Fact]
    public void Template_strategy_overrides_are_copied_and_cannot_be_mutated_after_registration()
    {
        Dictionary<int, NpcStrategyProfile> overrides = new()
        {
            [100] = NpcStrategyProfileResolver.Survival
        };
        NpcStrategyProfileResolver resolver = new(overrides);
        overrides[100] = NpcStrategyProfileResolver.AggressivePressure;
        NpcPerceptionSnapshot perception = CreatePerception();
        NpcIntelligenceProfile intelligence = NpcIntelligenceProfileResolver.Instance
            .Resolve(perception.State.Identity);

        resolver.Resolve(perception.State.Identity, intelligence)
            .Should().BeSameAs(NpcStrategyProfileResolver.Survival);
    }

    [Fact]
    public void Ranged_strategy_approaches_to_its_profile_range()
    {
        NpcSkillObservation skill = new(107, 2, 800, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 1_000)], skills: [skill], legacyAiType: LegacyNpcAiType.Mage);

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(perception, Context(),
            NpcStrategyProfileResolver.RangedControl);

        decision.Strategy.Should().Be(NpcStrategyArchetype.RangedControl);
        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ApproachTargetIntent>()
            .Which.PreferredRange.Should().Be(600);
    }

    [Fact]
    public void Survival_strategy_prefers_an_early_heal_over_an_offensive_skill()
    {
        NpcSkillObservation heal = new(205, 1, 0, 10, NpcSkillCategory.Heal,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcSkillObservation offensive = new(107, 2, 600, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [offensive, heal], hp: 40);

        CastSkillIntent intent = (CastSkillIntent)new NpcBrainCoordinator().DecideWithStrategy(perception,
            new NpcBrainContext(NpcBrainStimulus.PeriodicDue),
            NpcStrategyProfileResolver.Survival).Intents.Single();

        intent.SkillId.Should().Be(205);
        intent.Target.Should().Be(new EntityKey(Actor.ObjectId, Actor.Generation, EntityKind.Npc));
    }

    [Fact]
    public void Aggressive_strategy_lowers_flee_threshold_without_bypassing_reflex_authority()
    {
        NpcIntelligenceProfile intelligence = new(NpcIntelligenceArchetype.BasicMeleeMob,
            true, true, true, true, 20, 0, 200);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], hp: 10);

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(perception,
            new NpcBrainContext(NpcBrainStimulus.Attacked, intelligence),
            NpcStrategyProfileResolver.AggressivePressure);

        decision.Strategy.Should().Be(NpcStrategyArchetype.AggressivePressure);
        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Caster_without_ready_skill_approaches_physical_attack_range()
    {
        NpcIntelligenceProfile profile = new(NpcIntelligenceArchetype.BasicCasterMob,
            true, true, true, false, 0, 400, 1500);
        NpcSkillObservation cooldownSkill = new(107, 2, 600, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Cooldown);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 300)], skills: [cooldownSkill]);

        ApproachTargetIntent intent = (ApproachTargetIntent)new NpcBrainCoordinator().Decide(perception,
            new NpcBrainContext(NpcBrainStimulus.PeriodicDue, profile)).Intents.Single();

        intent.PreferredRange.Should().Be(40);
    }

    [Fact]
    public void Ready_offensive_skill_in_range_is_selected_deterministically()
    {
        NpcSkillObservation skill = new(107, 2, 600, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 300)], skills: [skill]);

        NpcIntent intent = new NpcBrainCoordinator().Decide(perception, Context()).Intents.Single();

        intent.Should().BeOfType<CastSkillIntent>().Which.Should().Match<CastSkillIntent>(cast =>
            cast.SkillId == 107 && cast.SkillLevel == 2 && cast.Target == Player);
    }

    [Theory]
    [InlineData(NpcStrategyArchetype.Balanced)]
    [InlineData(NpcStrategyArchetype.AggressivePressure)]
    [InlineData(NpcStrategyArchetype.RangedControl)]
    [InlineData(NpcStrategyArchetype.Survival)]
    public void Consecutive_ready_offensive_skills_are_suppressed_when_basic_attack_is_in_range(
        NpcStrategyArchetype archetype)
    {
        NpcSkillObservation skill = new(4247, 1, 1_000, 13, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcBrainCoordinator brain = new();
        NpcStrategyProfile strategy = NpcStrategyProfileResolver.ResolveArchetype(archetype, out _);

        NpcIntent[] sequence = Enumerable.Range(1, 4).Select(revision =>
            brain.DecideWithStrategy(CreatePerception(target: Player,
                    visible: [Hostile(Player, 30)], skills: [skill], revision: revision),
                Context(), strategy).Intents.Single()).ToArray();

        sequence.Select(intent => intent.Envelope.IntentType).Should().Equal(
            NpcIntentType.CastSkill, NpcIntentType.BasicAttack,
            NpcIntentType.CastSkill, NpcIntentType.BasicAttack);
    }

    [Fact]
    public void Phase3_baseline_also_suppresses_consecutive_offensive_casts_after_skilllist_fix()
    {
        NpcSkillObservation skill = new(4072, 1, 40, 10, NpcSkillCategory.Control,
            NpcSkillObservationFlags.Ready);
        NpcBrainCoordinator brain = new();

        NpcIntent first = brain.Decide(CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [skill]), Context()).Intents.Single();
        NpcIntent second = brain.Decide(CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [skill], revision: 2), Context()).Intents.Single();

        first.Should().BeOfType<CastSkillIntent>();
        second.Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Repeated_ranged_cast_is_not_suppressed_when_basic_attack_is_out_of_range()
    {
        NpcSkillObservation skill = new(4247, 1, 1_000, 13, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcBrainCoordinator brain = new();

        NpcIntent first = brain.DecideWithStrategy(CreatePerception(target: Player,
                visible: [Hostile(Player, 500)], skills: [skill]), Context(),
            NpcStrategyProfileResolver.RangedControl).Intents.Single();
        NpcIntent second = brain.DecideWithStrategy(CreatePerception(target: Player,
                visible: [Hostile(Player, 500)], skills: [skill], revision: 2), Context(),
            NpcStrategyProfileResolver.RangedControl).Intents.Single();

        first.Should().BeOfType<CastSkillIntent>();
        second.Should().BeOfType<CastSkillIntent>();
    }

    [Fact]
    public void Replay_diagnostics_explain_melee_repeat_suppression()
    {
        NpcSkillObservation skill = new(4247, 1, 1_000, 13, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcBrainCoordinator brain = new();
        brain.DecideWithStrategy(CreatePerception(target: Player,
                visible: [Hostile(Player, 30)], skills: [skill]), Context(),
            NpcStrategyProfileResolver.RangedControl);

        NpcStrategyBrainEvaluation evaluation = brain.DecideWithStrategyDiagnostics(
            CreatePerception(target: Player, visible: [Hostile(Player, 30)],
                skills: [skill], revision: 2), Context(), NpcStrategyProfileResolver.RangedControl);

        evaluation.Decision.Intents.Single().Should().BeOfType<BasicAttackIntent>();
        Candidate(evaluation, NpcStrategyAction.OffensiveSkill).Eligibility
            .Should().Be(NpcStrategyCandidateEligibility.RepeatedActionSuppressed);
    }

    [Fact]
    public void Tactical_brain_emits_no_action_while_an_existing_cast_is_in_progress()
    {
        NpcSkillObservation skill = new(4247, 1, 1_000, 13, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcBrainCoordinator brain = new();

        NpcBrainDecision decision = brain.DecideWithStrategy(
            CreatePerception(target: Player, visible: [Hostile(Player, 500)], skills: [skill],
                combatFlags: NpcCombatFlags.Casting), Context(),
            NpcStrategyProfileResolver.RangedControl);

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Casting_wakeup_preserves_last_valid_decision_for_the_next_melee_choice()
    {
        NpcSkillObservation skill = new(4247, 1, 1_000, 13, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcBrainCoordinator brain = new();

        NpcBrainDecision cast = brain.DecideWithStrategy(
            CreatePerception(target: Player, visible: [Hostile(Player, 30)], skills: [skill]),
            Context(), NpcStrategyProfileResolver.RangedControl);
        NpcBrainDecision whileCasting = brain.DecideWithStrategy(
            CreatePerception(target: Player, visible: [Hostile(Player, 30)], skills: [skill],
                revision: 2, combatFlags: NpcCombatFlags.Casting), Context(),
            NpcStrategyProfileResolver.RangedControl);
        NpcBrainDecision ready = brain.DecideWithStrategy(
            CreatePerception(target: Player, visible: [Hostile(Player, 30)], skills: [skill],
                revision: 3), Context(), NpcStrategyProfileResolver.RangedControl);

        cast.Intents.Single().Should().BeOfType<CastSkillIntent>();
        whileCasting.Intents.Should().BeEmpty();
        ready.Intents.Single().Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Target_inside_collision_adjusted_skill_reach_is_cast_on_instead_of_stalling()
    {
        NpcSkillObservation skill = new(107, 2, 100, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        VisibleEntity target = Hostile(Player, 115) with { CollisionRadius = 10 };
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [target], skills: [skill]);

        NpcIntent intent = new NpcBrainCoordinator().Decide(perception, Context()).Intents.Single();

        intent.Should().BeOfType<CastSkillIntent>().Which.Target.Should().Be(Player);
    }

    [Theory]
    [InlineData(NpcSkillObservationFlags.Cooldown)]
    [InlineData(NpcSkillObservationFlags.InsufficientMana)]
    public void Unavailable_skill_is_not_selected(NpcSkillObservationFlags flags)
    {
        NpcSkillObservation skill = new(107, 2, 600, 20, NpcSkillCategory.Offensive, flags);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [skill]);

        NpcIntent intent = new NpcBrainCoordinator().Decide(perception, Context()).Intents.Single();

        intent.Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Low_health_actor_uses_ready_heal()
    {
        NpcSkillObservation skill = new(205, 1, 0, 10, NpcSkillCategory.Heal,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [skill], hp: 20);

        CastSkillIntent intent = (CastSkillIntent)new NpcBrainCoordinator().Decide(perception, Context())
            .Intents.Single();

        intent.SkillId.Should().Be(205);
        intent.Target.Should().Be(new EntityKey(Actor.ObjectId, Actor.Generation, EntityKind.Npc));
    }

    [Fact]
    public void Actor_outside_leash_returns_home_without_target()
    {
        NpcPerceptionSnapshot perception = CreatePerception(position: new NpcPosition(500, 0, 0, 0),
            spawn: new NpcPosition(0, 0, 0, 0));

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ReturnHomeIntent>();
    }

    [Fact]
    public void First_soft_leash_crossing_gets_a_fixed_combat_grace_window()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30) with { Position = new NpcPosition(1_530, 0, 0, 0) }],
            threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
            position: new NpcPosition(1_501, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0),
            worldTick: 100);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, Context());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Soft_leash_grace_expires_without_being_renewed_by_ranged_damage()
    {
        NpcBrainCoordinator brain = new();
        NpcPosition spawn = new(0, 0, 0, 0);
        NpcPerceptionSnapshot crossed = CreatePerception(target: Player,
            visible: [Hostile(Player, 100) with { Position = new NpcPosition(1_700, 0, 0, 0) }],
            threats: [new ThreatEntry(Player, 100, 50, 100, true, true)],
            position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn, worldTick: 100);
        brain.Decide(crossed, Context(NpcBrainStimulus.Attacked)).Intents.Single()
            .Should().BeOfType<ApproachTargetIntent>();

        NpcPerceptionSnapshot expired = CreatePerception(target: Player,
            visible: [Hostile(Player, 100) with { Position = new NpcPosition(1_700, 0, 0, 0) }],
            threats: [new ThreatEntry(Player, 500, 250, 100, true, true)],
            position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn, revision: 2, worldTick: 300);

        ReturnHomeIntent intent = brain.Decide(expired, Context(NpcBrainStimulus.Attacked)).Intents.Single()
            .Should().BeOfType<ReturnHomeIntent>().Which;
        intent.Mode.Should().Be(NpcReturnHomeMode.PreserveThreat);
    }

    [Fact]
    public void Hard_leash_stops_the_grace_window_immediately()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 100) with { Position = new NpcPosition(2_101, 0, 0, 0) }],
            threats: [new ThreatEntry(Player, 100, 50, 100, true, true)],
            position: new NpcPosition(2_001, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0),
            worldTick: 100);

        ReturnHomeIntent intent = new NpcBrainCoordinator().Decide(perception, Context()).Intents.Single()
            .Should().BeOfType<ReturnHomeIntent>().Which;
        intent.Mode.Should().Be(NpcReturnHomeMode.PreserveThreat);
    }

    [Fact]
    public void Returning_actor_does_not_reacquire_visible_hostile()
    {
        NpcPerceptionSnapshot perception = CreatePerception(visible: [Hostile(Player, 30)],
            position: new NpcPosition(500, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0),
            returningToSpawn: true);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PlayerBecameRelevant));

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Returning_actor_interrupts_return_when_attacked_again()
    {
        NpcPerceptionSnapshot perception = CreatePerception(visible: [Hostile(Player, 30)],
            threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
            position: new NpcPosition(500, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0),
            returningToSpawn: true);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.Attacked));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Disabled_return_defense_keeps_the_hard_reset_behavior_outside_leash()
    {
        NpcPerceptionSnapshot perception = CreatePerception(visible: [Hostile(Player, 30)],
            threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
            position: new NpcPosition(1_600, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0),
            returningToSpawn: true);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            new NpcBrainContext(NpcBrainStimulus.Attacked,
                ReturnDefense: new NpcReturnDefensePolicy(false, 1200)));

        ReturnHomeIntent intent = decision.Intents.Single().Should().BeOfType<ReturnHomeIntent>().Which;
        intent.Mode.Should().Be(NpcReturnHomeMode.ResetCombat);
    }

    [Fact]
    public void Return_defense_attacks_in_range_without_extending_the_combat_leash()
    {
        NpcBrainCoordinator brain = new();
        NpcPerceptionSnapshot interruptedReturn = CreatePerception(visible: [Hostile(Player, 30)],
            threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
            position: new NpcPosition(1_600, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0),
            returningToSpawn: true, worldTick: 100);

        brain.Decide(interruptedReturn, Context(NpcBrainStimulus.Attacked)).Intents.Single()
            .Should().BeOfType<AcquireTargetIntent>();

        NpcPerceptionSnapshot retaliating = CreatePerception(target: Player, visible: [Hostile(Player, 30)],
            threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
            position: new NpcPosition(1_600, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0),
            revision: 2, worldTick: 101);

        brain.Decide(retaliating, Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>().Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Higher_hate_attacker_replaces_target_during_return_defense()
    {
        EntityKey challenger = new(9277, 0, EntityKind.Player);
        NpcBrainCoordinator brain = new();
        NpcPosition outsideLeash = new(1_600, 0, 0, 0);
        NpcPosition spawn = new(0, 0, 0, 0);
        brain.Decide(CreatePerception(visible: [Hostile(Player, 30)],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: outsideLeash,
                spawn: spawn, returningToSpawn: true, worldTick: 100),
            Context(NpcBrainStimulus.Attacked));

        NpcPerceptionSnapshot challenged = CreatePerception(target: Player,
            visible: [Hostile(Player, 30), Hostile(challenger, 35, 1)],
            threats:
            [
                new ThreatEntry(Player, 100, 50, 30, true, true),
                new ThreatEntry(challenger, 250, 200, 35, true, true)
            ],
            position: outsideLeash, spawn: spawn, revision: 2, worldTick: 110);

        AcquireTargetIntent retarget = brain.Decide(challenged,
                Context(NpcBrainStimulus.ThreatChanged)).Intents.Single()
            .Should().BeOfType<AcquireTargetIntent>().Which;
        retarget.Target.Should().Be(challenger);
        retarget.Mode.Should().Be(NpcTargetAcquisitionMode.PreserveMovement);

        NpcPerceptionSnapshot switched = CreatePerception(target: challenger,
            visible: [Hostile(Player, 30), Hostile(challenger, 35, 1)],
            threats:
            [
                new ThreatEntry(Player, 100, 50, 30, true, true),
                new ThreatEntry(challenger, 250, 200, 35, true, true)
            ],
            position: outsideLeash, spawn: spawn, revision: 3, worldTick: 111);
        brain.Decide(switched, Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>().Which.Target.Should().Be(challenger);
    }

    [Fact]
    public void Nearby_higher_hate_attacker_is_engaged_without_canceling_defensive_return_first()
    {
        EntityKey nearby = new(9277, 0, EntityKind.Player);
        NpcBrainCoordinator brain = new();
        NpcPosition spawn = new(0, 0, 0, 0);
        NpcPosition actor = new(1_600, 0, 0, 0);
        VisibleEntity archer = Hostile(Player, 100) with { Position = new NpcPosition(1_700, 0, 0, 0) };

        AcquireTargetIntent initial = brain.Decide(CreatePerception(visible: [archer],
                threats: [new ThreatEntry(Player, 100, 50, 100, true, true)], position: actor,
                spawn: spawn, returningToSpawn: true, worldTick: 100),
            Context(NpcBrainStimulus.Attacked)).Intents.Single().Should()
            .BeOfType<AcquireTargetIntent>().Which;
        initial.Mode.Should().Be(NpcTargetAcquisitionMode.PreserveMovement);

        brain.Decide(CreatePerception(target: Player, visible: [archer],
                threats: [new ThreatEntry(Player, 100, 50, 100, true, true)], position: actor,
                spawn: spawn, revision: 2, worldTick: 101), Context()).Intents.Single()
            .Should().BeOfType<ReturnHomeIntent>();

        VisibleEntity dagger = Hostile(nearby, 20, 1) with { Position = new NpcPosition(1_580, 0, 0, 0) };
        AcquireTargetIntent retarget = brain.Decide(CreatePerception(target: Player,
                visible: [archer, dagger], threats:
                [
                    new ThreatEntry(Player, 100, 50, 100, true, true),
                    new ThreatEntry(nearby, 250, 200, 20, true, true)
                ], position: actor, spawn: spawn, revision: 3, worldTick: 102),
            Context(NpcBrainStimulus.ThreatChanged)).Intents.Single().Should()
            .BeOfType<AcquireTargetIntent>().Which;
        retarget.Target.Should().Be(nearby);
        retarget.Mode.Should().Be(NpcTargetAcquisitionMode.PreserveMovement);

        brain.Decide(CreatePerception(target: nearby, visible: [archer, dagger], threats:
            [
                new ThreatEntry(Player, 100, 50, 100, true, true),
                new ThreatEntry(nearby, 250, 200, 20, true, true)
            ], position: actor, spawn: spawn, revision: 4, worldTick: 103), Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>().Which.Target.Should().Be(nearby);
    }

    [Fact]
    public void Return_defense_expires_after_the_legacy_timeout()
    {
        NpcBrainCoordinator brain = new();
        NpcPosition outsideLeash = new(1_600, 0, 0, 0);
        NpcPosition spawn = new(0, 0, 0, 0);
        brain.Decide(CreatePerception(visible: [Hostile(Player, 30)],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: outsideLeash,
                spawn: spawn, returningToSpawn: true, worldTick: 100),
            Context(NpcBrainStimulus.Attacked));

        NpcPerceptionSnapshot expired = CreatePerception(target: Player, visible: [Hostile(Player, 30)],
            threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: outsideLeash,
            spawn: spawn, revision: 2, worldTick: 1_301);

        ReturnHomeIntent intent = brain.Decide(expired, Context()).Intents.Single()
            .Should().BeOfType<ReturnHomeIntent>().Which;
        intent.Mode.Should().Be(NpcReturnHomeMode.ResetCombat);
    }

    [Fact]
    public void Return_defense_approaches_only_when_target_is_toward_spawn()
    {
        NpcBrainCoordinator brain = new();
        NpcPosition spawn = new(0, 0, 0, 0);
        brain.Decide(CreatePerception(visible: [Hostile(Player, 30) with
                { Position = new NpcPosition(1_570, 0, 0, 0) }],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
                position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn,
                returningToSpawn: true, worldTick: 100),
            Context(NpcBrainStimulus.Attacked));

        NpcPerceptionSnapshot homeward = CreatePerception(target: Player,
            visible: [Hostile(Player, 100) with { Position = new NpcPosition(1_500, 0, 0, 0) }],
            threats: [new ThreatEntry(Player, 100, 50, 100, true, true)],
            position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn, revision: 2, worldTick: 101);

        ApproachTargetIntent intent = brain.Decide(homeward, Context()).Intents.Single()
            .Should().BeOfType<ApproachTargetIntent>().Which;
        intent.Constraint.Should().Be(NpcApproachConstraint.TowardSpawnOnly);
    }

    [Fact]
    public void Return_defense_continues_home_without_discarding_hate_when_target_is_outward()
    {
        NpcBrainCoordinator brain = new();
        NpcPosition spawn = new(0, 0, 0, 0);
        brain.Decide(CreatePerception(visible: [Hostile(Player, 30) with
                { Position = new NpcPosition(1_570, 0, 0, 0) }],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
                position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn,
                returningToSpawn: true, worldTick: 100),
            Context(NpcBrainStimulus.Attacked));

        NpcPerceptionSnapshot outward = CreatePerception(target: Player,
            visible: [Hostile(Player, 100) with { Position = new NpcPosition(1_700, 0, 0, 0) }],
            threats: [new ThreatEntry(Player, 100, 50, 100, true, true)],
            position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn, revision: 2, worldTick: 101);

        ReturnHomeIntent intent = brain.Decide(outward, Context()).Intents.Single()
            .Should().BeOfType<ReturnHomeIntent>().Which;
        intent.Mode.Should().Be(NpcReturnHomeMode.PreserveThreat);

        NpcPerceptionSnapshot returning = CreatePerception(target: Player,
            visible: [Hostile(Player, 100) with { Position = new NpcPosition(1_700, 0, 0, 0) }],
            threats: [new ThreatEntry(Player, 100, 50, 100, true, true)],
            position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn,
            returningToSpawn: true, revision: 3, worldTick: 102);
        brain.Decide(returning, Context()).Intents.Should().BeEmpty();
    }

    [Fact]
    public void Return_defense_does_not_resume_outward_chase_until_target_reenters_leash()
    {
        NpcBrainCoordinator brain = new();
        NpcPosition spawn = new(0, 0, 0, 0);
        brain.Decide(CreatePerception(visible: [Hostile(Player, 30) with
                { Position = new NpcPosition(1_630, 0, 0, 0) }],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
                position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn,
                returningToSpawn: true, worldTick: 100),
            Context(NpcBrainStimulus.Attacked));

        NpcPerceptionSnapshot actorInsideTargetOutside = CreatePerception(target: Player,
            visible: [Hostile(Player, 110) with { Position = new NpcPosition(1_600, 0, 0, 0) }],
            threats: [new ThreatEntry(Player, 100, 50, 110, true, true)],
            position: new NpcPosition(1_490, 0, 0, 0), spawn: spawn, revision: 2, worldTick: 101);

        ReturnHomeIntent intent = brain.Decide(actorInsideTargetOutside, Context()).Intents.Single()
            .Should().BeOfType<ReturnHomeIntent>().Which;
        intent.Mode.Should().Be(NpcReturnHomeMode.PreserveThreat);
    }

    [Fact]
    public void Third_soft_leash_excursion_requests_teleport_and_combat_reset()
    {
        NpcBrainCoordinator brain = new();
        NpcPosition spawn = new(0, 0, 0, 0);
        NpcPosition outsideActor = new(1_600, 0, 0, 0);
        VisibleEntity outsideTarget = Hostile(Player, 30) with
        {
            Position = new NpcPosition(1_630, 0, 0, 0)
        };
        NpcPosition insideActor = new(1_400, 0, 0, 0);
        VisibleEntity insideTarget = Hostile(Player, 30) with
        {
            Position = new NpcPosition(1_430, 0, 0, 0)
        };

        brain.Decide(CreatePerception(target: Player, visible: [outsideTarget],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: outsideActor,
                spawn: spawn, worldTick: 100), Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>();
        brain.Decide(CreatePerception(target: Player, visible: [insideTarget],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: insideActor,
                spawn: spawn, revision: 2, worldTick: 101), Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>();

        brain.Decide(CreatePerception(target: Player, visible: [outsideTarget],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: outsideActor,
                spawn: spawn, revision: 3, worldTick: 102), Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>();
        brain.Decide(CreatePerception(target: Player, visible: [insideTarget],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: insideActor,
                spawn: spawn, revision: 4, worldTick: 103), Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>();

        ReturnHomeIntent emergency = brain.Decide(CreatePerception(target: Player,
                visible: [outsideTarget], threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
                position: outsideActor, spawn: spawn, revision: 5, worldTick: 104), Context()).Intents.Single()
            .Should().BeOfType<ReturnHomeIntent>().Which;
        emergency.Mode.Should().Be(NpcReturnHomeMode.TeleportReset);
    }

    [Fact]
    public void New_damage_renews_the_legacy_return_defense_timeout()
    {
        NpcBrainCoordinator brain = new();
        NpcPosition position = new(1_600, 0, 0, 0);
        NpcPosition spawn = new(0, 0, 0, 0);
        brain.Decide(CreatePerception(visible: [Hostile(Player, 30)],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: position,
                spawn: spawn, returningToSpawn: true, worldTick: 100),
            Context(NpcBrainStimulus.Attacked));

        NpcPerceptionSnapshot renewed = CreatePerception(target: Player, visible: [Hostile(Player, 30)],
            threats: [new ThreatEntry(Player, 200, 100, 30, true, true)], position: position,
            spawn: spawn, revision: 2, worldTick: 1_299);
        brain.Decide(renewed, Context(NpcBrainStimulus.ThreatChanged)).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>();

        NpcPerceptionSnapshot afterOriginalDeadline = renewed with
        {
            Envelope = renewed.Envelope with { StateRevision = 3, WorldTick = 1_301 }
        };
        brain.Decide(afterOriginalDeadline, Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Zero_combat_leash_remains_unbounded_instead_of_using_profile_leash()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [Hostile(Player, 30)],
            threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
            position: new NpcPosition(5_000, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0),
            combatLeashDistance: 0);

        new NpcBrainCoordinator().Decide(perception, Context()).Intents.Single()
            .Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Reaching_home_during_return_defense_clears_combat_before_reacquisition()
    {
        NpcBrainCoordinator brain = new();
        NpcPosition spawn = new(0, 0, 0, 0);
        brain.Decide(CreatePerception(visible: [Hostile(Player, 30)],
                threats: [new ThreatEntry(Player, 100, 50, 30, true, true)],
                position: new NpcPosition(1_600, 0, 0, 0), spawn: spawn,
                returningToSpawn: true, worldTick: 100),
            Context(NpcBrainStimulus.Attacked));

        NpcPerceptionSnapshot atHome = CreatePerception(target: Player, visible: [Hostile(Player, 30)],
            threats: [new ThreatEntry(Player, 100, 50, 30, true, true)], position: spawn,
            spawn: spawn, revision: 2, worldTick: 101);

        brain.Decide(atHome, Context()).Intents.Single().Should().BeOfType<StopCombatIntent>();
    }

    [Fact]
    public void Actor_at_home_reacquires_hostile_still_inside_aggro_range()
    {
        NpcPerceptionSnapshot perception = CreatePerception(visible: [Hostile(Player, 30)],
            position: new NpcPosition(0, 0, 0, 0), spawn: new NpcPosition(0, 0, 0, 0));

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            Context(NpcBrainStimulus.PeriodicDue));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player);
    }

    [Fact]
    public void Low_health_actor_flees_when_profile_allows_it()
    {
        NpcIntelligenceProfile profile = new(NpcIntelligenceArchetype.BasicMeleeMob,
            true, true, true, true, 20, 0, 200);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player, visible: [Hostile(Player, 30)], hp: 10);

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception,
            new NpcBrainContext(NpcBrainStimulus.Attacked, profile));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<FleeIntent>();
    }

    [Fact]
    public void New_generation_replaces_state_and_stale_generation_cannot_replace_it_back()
    {
        NpcBrainCoordinator brain = new();
        NpcPerceptionSnapshot generation4 = CreatePerception();
        NpcPerceptionSnapshot generation5 = CreatePerception(actor: Actor with { Generation = 5 });

        brain.Decide(generation4, Context()).DecisionSequence.Should().Be(1);
        brain.Decide(generation4, Context()).DecisionSequence.Should().Be(2);
        brain.Decide(generation5, Context()).DecisionSequence.Should().Be(1);
        brain.Decide(generation4, Context()).DecisionSequence.Should().Be(0);
    }

    [Fact]
    public void Replay_of_same_perception_sequence_is_semantically_deterministic()
    {
        NpcPerceptionSnapshot[] replay =
        [
            CreatePerception(visible: [Hostile(Player, 80)]),
            CreatePerception(target: Player, visible: [Hostile(Player, 30)], revision: 2),
            CreatePerception(target: Player,
                visible: [Hostile(Player, 30) with { State = EntityStateFlags.Spawned | EntityStateFlags.AlikeDead }],
                revision: 3)
        ];
        NpcBrainCoordinator first = new();
        NpcBrainCoordinator second = new();

        NpcIntent[] firstRun = replay.SelectMany(frame => first.Decide(frame, Context()).Intents).ToArray();
        NpcIntent[] secondRun = replay.SelectMany(frame => second.Decide(frame, Context()).Intents).ToArray();

        firstRun.Should().HaveCount(3);
        firstRun.Zip(secondRun).Should().OnlyContain(pair =>
            NpcIntentSemanticComparer.Instance.Equals(pair.First, pair.Second));
    }

    [Fact]
    public void Replay_runner_produces_repeatable_intent_sequence_without_gameserver()
    {
        NpcPerceptionSnapshot[] replay =
        [
            CreatePerception(visible: [Hostile(Player, 80)]),
            CreatePerception(target: Player, visible: [Hostile(Player, 30)], revision: 2)
        ];
        NpcBrainReplayRunner runner = new();

        NpcIntent[] first = runner.Run(replay).ToArray();
        NpcIntent[] second = runner.Run(replay).ToArray();

        first.Should().HaveCount(2);
        first.Zip(second).Should().OnlyContain(pair =>
            NpcIntentSemanticComparer.Instance.Equals(pair.First, pair.Second));
    }

    [Fact]
    public void Comparative_replay_evaluates_every_profile_against_each_revision_with_opt_in_diagnostics()
    {
        NpcSkillObservation heal = new(205, 1, 0, 10, NpcSkillCategory.Heal,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcSkillObservation offensive = new(107, 2, 600, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [offensive, heal], hp: 40, revision: 812);

        StrategyReplayResult[] results = new NpcBrainReplayRunner().RunComparative([perception]).ToArray();

        results.Should().HaveCount(4);
        results.Should().OnlyContain(result => result.SnapshotRevision == 812 &&
            result.DecisionDuration >= TimeSpan.Zero && !result.CandidateScores.IsEmpty);
        results.Select(result => result.Profile).Should().BeEquivalentTo(
            Enum.GetValues<NpcStrategyArchetype>());
        results.Single(result => result.Profile == NpcStrategyArchetype.Balanced)
            .AppliedModifiers.Should().BeEmpty();
        results.Single(result => result.Profile == NpcStrategyArchetype.Survival)
            .SelectedAction.Should().Be(NpcStrategyAction.Heal);
        results.Single(result => result.Profile == NpcStrategyArchetype.Survival)
            .SelectedIntent.Should().BeOfType<CastSkillIntent>().Which.SkillId.Should().Be(205);
    }

    [Fact]
    public void Balanced_strategy_is_an_identity_profile_against_the_phase3_pipeline()
    {
        NpcSkillObservation offensive = new(107, 2, 600, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot[] replay =
        [
            CreatePerception(visible: [Hostile(Player, 80)], skills: [offensive]),
            CreatePerception(target: Player, visible: [Hostile(Player, 300)], skills: [offensive], revision: 2),
            CreatePerception(target: Player, visible: [Hostile(Player, 30)], skills: [offensive], revision: 3)
        ];
        NpcBrainCoordinator baseline = new();
        NpcBrainCoordinator balanced = new();

        foreach (NpcPerceptionSnapshot perception in replay)
        {
            NpcBrainDecision baselineDecision = baseline.Decide(perception, Context());
            NpcBrainDecision balancedDecision = balanced.DecideWithStrategy(perception, Context(),
                NpcStrategyProfileResolver.Balanced);

            baselineDecision.Intents.Should().HaveSameCount(balancedDecision.Intents);
            baselineDecision.Intents.Zip(balancedDecision.Intents).Should().OnlyContain(pair =>
                NpcIntentSemanticComparer.Instance.Equals(pair.First, pair.Second));
            balancedDecision.StrategyDecision.Should().NotBeNull();
            balancedDecision.StrategyDecision!.Value.AppliedModifiers
                .Should().Be(NpcStrategyModifierFlags.None);
        }
    }

    [Fact]
    public void Online_shadow_uses_the_same_predecision_state_and_persists_only_baseline_state()
    {
        NpcBrainStateStore states = new();
        NpcBrainCoordinator shadowBrain = new(states, NpcIntelligenceProfileResolver.Instance,
            new StrategyBrain(), new ReflexBrain(), new TacticalBrain());
        NpcBrainCoordinator baselineBrain = new();
        NpcSkillObservation heal = new(205, 1, 0, 10, NpcSkillCategory.Heal,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcSkillObservation offensive = new(107, 2, 600, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot[] replay =
        [
            CreatePerception(target: Player, visible: [Hostile(Player, 30)],
                skills: [offensive, heal], hp: 40),
            CreatePerception(target: Player, visible: [Hostile(Player, 30)],
                skills: [offensive, heal], hp: 40, revision: 2)
        ];

        foreach (NpcPerceptionSnapshot perception in replay)
        {
            NpcStrategyShadowEvaluation shadow = shadowBrain.DecideShadow(perception, Context(),
                NpcStrategyProfileResolver.Survival);
            NpcBrainDecision baseline = baselineBrain.Decide(perception, Context());

            shadow.StrategyFailed.Should().BeFalse();
            shadow.StrategyDecision.Should().NotBeNull();
            shadow.BaselineDecision.DecisionSequence.Should().Be(baseline.DecisionSequence);
            shadow.StrategyDecision!.DecisionSequence.Should().Be(baseline.DecisionSequence);
            shadow.BaselineDecision.Intents.Zip(baseline.Intents).Should().OnlyContain(pair =>
                NpcIntentSemanticComparer.Instance.Equals(pair.First, pair.Second));
        }

        NpcBrainStateSnapshot state = states.GetSnapshot(Actor.ObjectId)!.Value;
        state.DecisionSequence.Should().Be(2);
        state.LastStrategy.Should().Be(NpcStrategyArchetype.Balanced);
    }

    [Fact]
    public void S1_melee_with_offensive_skill_preserves_profile_score_invariants()
    {
        NpcSkillObservation offensive = ReadySkill(107, NpcSkillCategory.Offensive, 600);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [offensive]);
        Dictionary<NpcStrategyArchetype, NpcStrategyBrainEvaluation> results = EvaluateAll(perception);

        Score(results, NpcStrategyArchetype.AggressivePressure, NpcStrategyAction.BasicAttack)
            .Should().BeGreaterThanOrEqualTo(Score(results, NpcStrategyArchetype.Balanced,
                NpcStrategyAction.BasicAttack));
        Score(results, NpcStrategyArchetype.AggressivePressure, NpcStrategyAction.OffensiveSkill)
            .Should().BeGreaterThanOrEqualTo(Score(results, NpcStrategyArchetype.Balanced,
                NpcStrategyAction.OffensiveSkill));
        results.Values.Should().OnlyContain(result =>
            result.Decision.Intents.Single() is CastSkillIntent);
    }

    [Fact]
    public void S2_low_hp_with_heal_makes_survival_at_least_as_conservative_as_balanced()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], hp: 20,
            skills: [ReadySkill(205, NpcSkillCategory.Heal),
                ReadySkill(107, NpcSkillCategory.Offensive, 600)]);
        Dictionary<NpcStrategyArchetype, NpcStrategyBrainEvaluation> results = EvaluateAll(perception);

        Score(results, NpcStrategyArchetype.Survival, NpcStrategyAction.Heal)
            .Should().BeGreaterThanOrEqualTo(Score(results, NpcStrategyArchetype.Balanced,
                NpcStrategyAction.Heal));
        results[NpcStrategyArchetype.Survival].Decision.StrategyDecision!.Value.SelectedAction
            .Should().Be(NpcStrategyAction.Heal);
    }

    [Fact]
    public void S3_ranged_target_in_skill_range_favors_ranged_control_utility()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 500)], skills: [ReadySkill(107, NpcSkillCategory.Offensive, 600)]);
        Dictionary<NpcStrategyArchetype, NpcStrategyBrainEvaluation> results = EvaluateAll(perception);

        Score(results, NpcStrategyArchetype.RangedControl, NpcStrategyAction.OffensiveSkill)
            .Should().BeGreaterThanOrEqualTo(Score(results, NpcStrategyArchetype.Balanced,
                NpcStrategyAction.OffensiveSkill));
        results[NpcStrategyArchetype.RangedControl].Decision.Intents.Single()
            .Should().BeOfType<CastSkillIntent>();
    }

    [Fact]
    public void S4_target_outside_every_range_never_becomes_an_executable_skill_intent()
    {
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 900)], skills: [ReadySkill(107, NpcSkillCategory.Offensive, 600)]);

        EvaluateAll(perception).Values.Should().OnlyContain(result =>
            result.Decision.Intents.Single() is ApproachTargetIntent &&
            Candidate(result, NpcStrategyAction.OffensiveSkill).Eligibility ==
                NpcStrategyCandidateEligibility.OutOfRange);
    }

    [Fact]
    public void S5_offensive_skill_on_cooldown_is_ineligible_for_every_profile()
    {
        NpcSkillObservation cooldown = new(107, 1, 600, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.Cooldown | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [cooldown]);

        EvaluateAll(perception).Values.Should().OnlyContain(result =>
            result.Decision.Intents.Single() is BasicAttackIntent &&
            Candidate(result, NpcStrategyAction.OffensiveSkill).Eligibility ==
                NpcStrategyCandidateEligibility.SkillUnavailable);
    }

    [Fact]
    public void S6_offensive_skill_with_insufficient_mana_is_ineligible_for_every_profile()
    {
        NpcSkillObservation noMana = new(107, 1, 600, 20, NpcSkillCategory.Offensive,
            NpcSkillObservationFlags.InsufficientMana | NpcSkillObservationFlags.Magic);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], skills: [noMana]);

        EvaluateAll(perception).Values.Should().OnlyContain(result =>
            result.Decision.Intents.Single() is BasicAttackIntent &&
            Candidate(result, NpcStrategyAction.OffensiveSkill).Eligibility ==
                NpcStrategyCandidateEligibility.SkillUnavailable);
    }

    [Fact]
    public void S7_flee_is_strengthened_only_when_the_tactical_profile_allows_it()
    {
        NpcIntelligenceProfile canFlee = new(NpcIntelligenceArchetype.BasicMeleeMob,
            true, true, true, true, 20, 0, 200);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], hp: 15);
        Dictionary<NpcStrategyArchetype, NpcStrategyBrainEvaluation> results = EvaluateAll(perception,
            new NpcBrainContext(NpcBrainStimulus.Attacked, canFlee));

        StrategyBrain strategyBrain = new();
        TacticalActionEvaluator evaluator = new();
        int balancedFlee = evaluator.Evaluate(perception, canFlee,
            strategyBrain.Decide(perception, canFlee, NpcStrategyProfileResolver.Balanced), 30).Score;
        int survivalFlee = evaluator.Evaluate(perception, canFlee,
            strategyBrain.Decide(perception, canFlee, NpcStrategyProfileResolver.Survival), 30).Score;

        survivalFlee.Should().BeGreaterThanOrEqualTo(balancedFlee);
        results[NpcStrategyArchetype.Survival].Decision.Intents.Single().Should().BeOfType<FleeIntent>();
    }

    [Theory]
    [InlineData(NpcStrategyArchetype.Balanced)]
    [InlineData(NpcStrategyArchetype.AggressivePressure)]
    [InlineData(NpcStrategyArchetype.RangedControl)]
    [InlineData(NpcStrategyArchetype.Survival)]
    public void Same_input_profile_and_initial_state_is_exactly_deterministic_for_1000_runs(
        NpcStrategyArchetype archetype)
    {
        NpcStrategyProfile strategy = NpcStrategyProfileResolver.ResolveArchetype(archetype, out _);
        NpcPerceptionSnapshot perception = CreatePerception(target: Player,
            visible: [Hostile(Player, 30)], hp: 20,
            skills: [ReadySkill(205, NpcSkillCategory.Heal),
                ReadySkill(107, NpcSkillCategory.Offensive, 600)]);
        NpcBrainDecision expected = new NpcBrainCoordinator().DecideWithStrategy(
            perception, Context(), strategy);

        for (int iteration = 0; iteration < 1000; iteration++)
        {
            NpcBrainDecision actual = new NpcBrainCoordinator().DecideWithStrategy(
                perception, Context(), strategy);
            actual.Actor.Should().Be(expected.Actor);
            actual.DecisionSequence.Should().Be(expected.DecisionSequence);
            actual.Layer.Should().Be(expected.Layer);
            actual.StrategyDecision.Should().Be(expected.StrategyDecision);
            actual.Intents.Should().Equal(expected.Intents);
        }
    }

    [Theory]
    [InlineData(NpcStrategyArchetype.Balanced)]
    [InlineData(NpcStrategyArchetype.AggressivePressure)]
    [InlineData(NpcStrategyArchetype.RangedControl)]
    [InlineData(NpcStrategyArchetype.Survival)]
    public void Strategy_replay_sequence_is_exactly_deterministic(NpcStrategyArchetype archetype)
    {
        NpcStrategyProfile strategy = NpcStrategyProfileResolver.ResolveArchetype(archetype, out _);
        NpcPerceptionSnapshot[] replay =
        [
            CreatePerception(visible: [Hostile(Player, 80)]),
            CreatePerception(target: Player, visible: [Hostile(Player, 500)],
                skills: [ReadySkill(107, NpcSkillCategory.Offensive, 600)], revision: 2),
            CreatePerception(target: Player, visible: [Hostile(Player, 30)], hp: 20,
                skills: [ReadySkill(205, NpcSkillCategory.Heal)], revision: 3)
        ];
        NpcBrainCoordinator first = new();
        NpcBrainCoordinator second = new();

        NpcBrainDecision[] firstRun = replay.Select(frame =>
            first.DecideWithStrategy(frame, Context(), strategy)).ToArray();
        NpcBrainDecision[] secondRun = replay.Select(frame =>
            second.DecideWithStrategy(frame, Context(), strategy)).ToArray();

        firstRun.Zip(secondRun).Should().OnlyContain(pair =>
            pair.First.Actor == pair.Second.Actor &&
            pair.First.DecisionSequence == pair.Second.DecisionSequence &&
            pair.First.Layer == pair.Second.Layer &&
            pair.First.StrategyDecision == pair.Second.StrategyDecision &&
            pair.First.Intents.SequenceEqual(pair.Second.Intents, EqualityComparer<NpcIntent>.Default));
    }

    [Fact]
    public void Unknown_runtime_archetype_falls_back_to_balanced_without_throwing()
    {
        NpcStrategyProfile profile = NpcStrategyProfileResolver.ResolveArchetype(
            (NpcStrategyArchetype)999, out bool usedFallback);

        usedFallback.Should().BeTrue();
        profile.Should().BeSameAs(NpcStrategyProfileResolver.Balanced);
    }

    private static Dictionary<NpcStrategyArchetype, NpcStrategyBrainEvaluation> EvaluateAll(
        NpcPerceptionSnapshot perception, NpcBrainContext? context = null) =>
        Enum.GetValues<NpcStrategyArchetype>().ToDictionary(archetype => archetype, archetype =>
            new NpcBrainCoordinator().DecideWithStrategyDiagnostics(perception, context ?? Context(),
                NpcStrategyProfileResolver.ResolveArchetype(archetype, out _)));

    private static int Score(IReadOnlyDictionary<NpcStrategyArchetype, NpcStrategyBrainEvaluation> results,
        NpcStrategyArchetype profile, NpcStrategyAction action) =>
        Candidate(results[profile], action).EffectiveScore;

    private static NpcStrategyCandidateScore Candidate(NpcStrategyBrainEvaluation result,
        NpcStrategyAction action) => result.Diagnostics.CandidateScores
        .Where(candidate => candidate.Action == action)
        .OrderByDescending(candidate => candidate.EffectiveScore)
        .First();

    private static NpcSkillObservation ReadySkill(int id, NpcSkillCategory category, int range = 0) =>
        new(id, 1, range, 10, category,
            NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic);

    private static NpcBrainContext Context(NpcBrainStimulus stimuli = NpcBrainStimulus.PeriodicDue) => new(stimuli);

    private static VisibleEntity Hostile(EntityKey key, double distance, int ordinal = 0) =>
        new(ordinal, key, new NpcPosition((int)distance, 0, 0, 0), 20, 5, 10, distance,
            EntityStateFlags.Alive | EntityStateFlags.Spawned,
            EntityRelationFlags.Player | EntityRelationFlags.Playable | EntityRelationFlags.SameInstance |
            EntityRelationFlags.AutoAttackable);

    private static NpcPerceptionSnapshot CreatePerception(
        NpcKey? actor = null,
        EntityKey? target = null,
        ImmutableArray<VisibleEntity> visible = default,
        ImmutableArray<ThreatEntry> threats = default,
        ImmutableArray<NpcSkillObservation> skills = default,
        NpcCapabilities capabilities = NpcCapabilities.CanMove | NpcCapabilities.CanAttack | NpcCapabilities.Aggressive,
        NpcPhysicalFlags physicalFlags = NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned,
        NpcPosition? position = null,
        NpcPosition? spawn = null,
        bool returningToSpawn = false,
        double hp = 100,
        long revision = 1,
        long? worldTick = null,
        int combatLeashDistance = 1500,
        LegacyNpcAiType legacyAiType = LegacyNpcAiType.Fighter,
        NpcCombatFlags combatFlags = NpcCombatFlags.None)
    {
        NpcKey npc = actor ?? Actor;
        NpcIdentity identity = new(100, NpcKind.Monster, legacyAiType, 20, 300, [], capabilities);
        NpcPhysicalState physical = new(position ?? new NpcPosition(0, 0, 0, 0), hp, 100, 50, 50, 8, 16,
            physicalFlags);
        NpcCombatFacts combat = new(target, 40, 500, combatFlags);
        NpcEnvironment environment = new(new RegionKey(0, 1, 1), spawn, true, true, false,
            returningToSpawn, true, 300, combatLeashDistance);
        NpcPerceptionState state = new(identity, physical, combat, environment, visible, threats, [], [], skills);
        return new NpcPerceptionSnapshot(
            new NpcPerceptionEnvelope(1, npc, revision, worldTick ?? revision, revision), state);
    }
}
