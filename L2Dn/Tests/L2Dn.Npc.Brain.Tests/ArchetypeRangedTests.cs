using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// VAL-03 — Archetype Ranged / Kiting tests.
///
/// IMPORTANT — two sub-categories:
///
/// ✅ TESTEABLE AHORA: Tests that exercise existing behavior (Approach, BasicAttack,
///    CastSkill with RangedControl profile). These use DecideWithStrategy() because
///    LegacyNpcAiType.Archer resolves internally to Aggressive (range 0); the
///    RangedControl profile applies PreferredRangeOverride = 600.
///
/// ⏭️ FORWARD-LOOKING (Skip): Tests that require MaintainRange / Retreat intents
///    which do not exist in NpcTacticalAction until Phase 4B.5. These are pre-written
///    to establish the future API contract.
/// </summary>
public class ArchetypeRangedTests
{
    private static readonly EntityKey Player1 = new(1001, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // ✅ Testeable now — uses DecideWithStrategy(RangedControl)
    //
    // NpcStrategyProfileResolver.RangedControl has PreferredRangeOverride = 600,
    // which sets DesiredRange in the tactical score. Without this profile, an
    // Archer NPC would use range 0 (Aggressive profile from LegacyAiType resolution).
    // -----------------------------------------------------------------------

    [Fact]
    public void Archer_pursues_target_at_extreme_range()
    {
        // Target at 1200 u > preferred range of 600 → should approach.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateArcher()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 1200)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.RangedControl);

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<ApproachTargetIntent>()
            .Which.Target.Should().Be(Player1);
    }

    [Fact]
    public void Mage_prioritizes_cast_skill_over_physical_attack()
    {
        // Mage with offensive magic skill ready and target in casting range.
        // TacticalBrain gives OffensiveSkillScore priority via RangedControl profile.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMage()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 400)
            .WithSkillReady(id: 201, NpcSkillCategory.Offensive, range: 600,
                flags: NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.ActionReady(), NpcStrategyProfileResolver.RangedControl);

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<CastSkillIntent>();
    }

    [Fact]
    public void Ranged_attacks_target_in_optimal_range()
    {
        // Target at 550 u — within preferred range of 600; NPC should attack, not approach.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateArcher()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 550)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.RangedControl);

        // BasicAttack or CastSkill; crucially NOT ApproachTargetIntent.
        decision.Intents.Should().ContainSingle();
        (decision.Intents[0] is BasicAttackIntent or CastSkillIntent).Should().BeTrue();
        decision.Intents.Should().NotContain(i => i is ApproachTargetIntent);
    }

    [Fact]
    public void Ranged_npc_acquires_hostile_on_aggro_stimulus()
    {
        // No current target; hostile in aggro range → AcquireTargetIntent.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateArcher()
            .WithHostileAt(Player1, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Aggro(), NpcStrategyProfileResolver.RangedControl);

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player1);
    }

    // -----------------------------------------------------------------------
    // ⏭️ Forward-looking — requires Phase 4B.5 (MaintainRange / Retreat)
    // -----------------------------------------------------------------------

    [Fact(Skip = "Requiere 4B.5 — MaintainRange/Retreat no existen en NpcTacticalAction")]
    public void Archer_kites_when_target_is_too_close()
    {
        // Target at < 200 u → NPC should retreat to preferred range.
        // Requires NpcTacticalAction.Retreat (added in Phase 4B.5).
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateArcher()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 50)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.RangedControl);

        // Expected: FleeIntent or a future RetreatIntent — not ApproachTargetIntent.
        decision.Intents.Should().ContainSingle()
            .Which.Should().NotBeOfType<ApproachTargetIntent>();
    }

    [Fact(Skip = "Requiere 4B.5 — kiting reactivo (MaintainRange) no existe aún")]
    public void Ranged_npc_uses_skill_while_kiting_gap()
    {
        // While retreating, a long-range skill should still fire (not mutually exclusive).
        // Requires Phase 4B.5 movement primitives.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateArcher()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 50)
            .WithSkillReady(id: 202, NpcSkillCategory.Offensive, range: 600,
                flags: NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.RangedControl);

        decision.Intents.Should().Contain(i => i is CastSkillIntent);
    }
}
