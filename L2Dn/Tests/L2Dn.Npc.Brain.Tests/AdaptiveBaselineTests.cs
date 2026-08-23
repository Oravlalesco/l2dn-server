using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// NPC-4B5-01 — Behavioral baseline guard (4B5-A1).
///
/// Freezes the exact decisions produced by Phase 4B (Static Strategy V1) across a
/// representative matrix of styles × situations. Any future 4B.5 subfase that causes
/// NPC_STRATEGY_ADAPTIVE_MODE=Disabled to diverge from Phase 4B behavior will break
/// these tests before reaching Docker.
///
/// Env-var / options tests live in L2Dn.GameServer.Model.Tests/NpcAdaptiveStrategyModeTests.cs
/// because NpcStrategyOptions is internal (InternalsVisibleTo excludes this project).
///
/// FLEE NOTE: All four NpcIntelligenceProfileResolver profiles have FleeAllowed=false.
/// Flee is only emitted when a custom NpcBrainContext with FleeAllowed=true is supplied.
/// Tests that exercise flee thresholds use <see cref="FleeAllowedContext"/> for that reason.
/// </summary>
public class AdaptiveBaselineTests
{
    private static readonly EntityKey Player = new(9001, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // Helper: NpcIntelligenceProfile with FleeAllowed=true for flee-threshold tests.
    // Archetype=BasicMeleeMob, ReflexEnabled=true, TacticalEnabled=true,
    // AcquireVisibleHostiles=true, FleeAllowed=true, FleeHpPercent=<param>, LeashDistance=200.
    // Matches the pattern established in NpcBrainTests.cs:1030.
    // -----------------------------------------------------------------------
    private static NpcBrainContext FleeAllowedContext(NpcBrainStimulus stimulus, double fleeHpPercent) =>
        new(stimulus, new NpcIntelligenceProfile(
            NpcIntelligenceArchetype.BasicMeleeMob,
            ReflexEnabled: true, TacticalEnabled: true,
            AcquireVisibleHostiles: true, FleeAllowed: true,
            FleeHpPercent: fleeHpPercent, PreferredRange: 0, LeashDistance: 200));

    // -----------------------------------------------------------------------
    // Baseline: Style=None (no strategy — raw Decide())
    // -----------------------------------------------------------------------

    [Fact]
    public void Baseline_NoStrategy_Idle_emits_no_intents()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee().Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().BeEmpty();
    }

    [Fact]
    public void Baseline_NoStrategy_TargetInMeleeRange_emits_BasicAttack()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 25)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Attacked());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Baseline_NoStrategy_TargetOutOfRange_emits_ApproachTarget()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ApproachTargetIntent>();
    }

    [Fact]
    public void Baseline_NoStrategy_FleeAllowedFalse_does_not_flee_at_any_hp()
    {
        // Default Fighter profile: FleeAllowed=false → Flee is never emitted regardless of HP.
        // This is the base invariant; flee tests below inject FleeAllowed=true explicitly.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.05) // 5% HP — would flee if FleeAllowed
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().NotContain(i => i is FleeIntent);
    }

    [Fact]
    public void Baseline_NoStrategy_TargetInvisible_emits_ClearTarget()
    {
        // Target set but NOT in visible entities → ClearTargetIntent.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .Build(); // No .WithHostileAt()

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ClearTargetIntent>();
    }

    [Fact]
    public void Baseline_NoStrategy_OutsideLeash_emits_ReturnHome()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithPosition(2000, 0, 0)
            .WithSpawnPosition(0, 0, 0)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 200)
            .WithCombatLeashDistance(1500)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ReturnHomeIntent>();
    }

    // -----------------------------------------------------------------------
    // Baseline: Style=Balanced (FleeHpPercent=25)
    // -----------------------------------------------------------------------

    [Fact]
    public void Baseline_Balanced_TargetInRange_emits_BasicAttack()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 25)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Attacked(), NpcStrategyProfileResolver.Balanced);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Baseline_Balanced_TargetOutOfRange_emits_ApproachTarget()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.Balanced);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ApproachTargetIntent>();
    }

    [Fact]
    public void Baseline_Balanced_FleeAt10pct_emits_Flee_when_FleeAllowed()
    {
        // Balanced: FleeHpPercent=35 (from NpcStrategyProfileResolver.Balanced).
        // At 10% HP with FleeAllowed=true → Flee (10 < 35).
        // Uses FleeAllowedContext because all resolver profiles have FleeAllowed=false.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.10)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, FleeAllowedContext(NpcBrainStimulus.PeriodicDue, fleeHpPercent: 35),
            NpcStrategyProfileResolver.Balanced);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<FleeIntent>();
    }

    // -----------------------------------------------------------------------
    // Baseline: Style=AggressivePressure (FleeHpPercent=5)
    // -----------------------------------------------------------------------

    [Fact]
    public void Baseline_AggressivePressure_TargetInRange_emits_BasicAttack()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 25)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Attacked(), NpcStrategyProfileResolver.AggressivePressure);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<BasicAttackIntent>();
    }

    [Fact]
    public void Baseline_AggressivePressure_does_not_flee_at_10pct_FleeHp_is_5pct()
    {
        // AggressivePressure: FleeHpPercent=5 (from resolver). At 10% HP with FleeAllowed=true
        // → NOT flee (10 > 5). This tests the threshold, not FleeAllowed=false.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.10)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, FleeAllowedContext(NpcBrainStimulus.PeriodicDue, fleeHpPercent: 5),
            NpcStrategyProfileResolver.AggressivePressure);

        decision.Intents.Should().ContainSingle().Which.Should().NotBeOfType<FleeIntent>();
    }

    // -----------------------------------------------------------------------
    // Baseline: Style=RangedControl
    // -----------------------------------------------------------------------

    [Fact]
    public void Baseline_RangedControl_TargetAtOptimalRange_emits_attack_not_approach()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateArcher()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 550)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.RangedControl);

        decision.Intents.Should().ContainSingle();
        (decision.Intents[0] is BasicAttackIntent or CastSkillIntent).Should().BeTrue();
        decision.Intents.Should().NotContain(i => i is ApproachTargetIntent);
    }

    [Fact]
    public void Baseline_RangedControl_TargetAtExtremeRange_emits_ApproachTarget()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateArcher()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 1200)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.RangedControl);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ApproachTargetIntent>();
    }

    // -----------------------------------------------------------------------
    // Baseline: Style=Survival (FleeHpPercent=30)
    // -----------------------------------------------------------------------

    [Fact]
    public void Baseline_Survival_flees_at_25pct_hp_when_FleeAllowed()
    {
        // Survival: FleeHpPercent=30 (from resolver). At 25% HP with FleeAllowed=true → Flee.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.25)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, FleeAllowedContext(NpcBrainStimulus.PeriodicDue, fleeHpPercent: 30),
            NpcStrategyProfileResolver.Survival);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<FleeIntent>();
    }

    [Fact]
    public void Baseline_Survival_does_not_flee_at_35pct_FleeHp_is_30pct()
    {
        // Survival: FleeHpPercent=30. At 35% HP with FleeAllowed=true → NOT flee (35 > 30).
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.35)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, FleeAllowedContext(NpcBrainStimulus.PeriodicDue, fleeHpPercent: 30),
            NpcStrategyProfileResolver.Survival);

        decision.Intents.Should().ContainSingle().Which.Should().NotBeOfType<FleeIntent>();
    }

    [Fact]
    public void Baseline_Survival_FullHp_TargetInRange_emits_BasicAttack()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(1.0)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 25)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Attacked(), NpcStrategyProfileResolver.Survival);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<BasicAttackIntent>();
    }
}
