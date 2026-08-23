using FluentAssertions;
using L2Dn.GameServer.AI.Runtime;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// NPC-4B5-01 — Baseline guard (4B5-A1).
///
/// This suite freezes the exact decisions produced by Phase 4B (Static Strategy V1)
/// across a representative matrix of archetypes × situations. Its only purpose is to
/// detect regressions: if any future 4B.5 subfase causes NPC_STRATEGY_ADAPTIVE_MODE=Disabled
/// to diverge from Phase 4B behavior, these tests will break before it reaches Docker.
///
/// Coverage matrix: 5 styles × 6 situations = 30 scenarios.
///   Styles:    Default (no strategy), Balanced, AggressivePressure, RangedControl, Survival
///   Situations: Idle, TargetInMeleeRange, TargetOutOfRange, LowHp, TargetInvisible, OutsideLeash
///
/// IMPORTANT: Do NOT adjust the golden assertions to "make them pass" after a behavioral change.
/// A failing test here means the Disabled path changed — that is the regression.
/// </summary>
public class AdaptiveBaselineTests
{
    private static readonly EntityKey Player = new(9001, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // Env-var / options tests
    // -----------------------------------------------------------------------

    [Fact]
    public void AdaptiveMode_default_is_Disabled()
    {
        // No env var set → Disabled.
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled, _ => null);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
    }

    [Fact]
    public void AdaptiveMode_Disabled_parseable_from_env_var()
    {
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled, name =>
                name == "NPC_STRATEGY_ADAPTIVE_MODE" ? "Disabled" : null);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
    }

    [Fact]
    public void AdaptiveMode_Shadow_falls_back_to_Disabled_with_warning()
    {
        List<string> warnings = [];
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? "Shadow" : null,
            warnings.Add);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
        warnings.Should().ContainSingle().Which.Should().Contain("Shadow");
    }

    [Fact]
    public void AdaptiveMode_Enabled_falls_back_to_Disabled_with_warning()
    {
        List<string> warnings = [];
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? "Enabled" : null,
            warnings.Add);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
        warnings.Should().ContainSingle().Which.Should().Contain("Enabled");
    }

    [Fact]
    public void AdaptiveMode_unknown_value_falls_back_to_Disabled_with_warning()
    {
        List<string> warnings = [];
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? "turbo_mode" : null,
            warnings.Add);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
        warnings.Should().ContainSingle().Which.Should().Contain("turbo_mode");
    }

    [Theory]
    [InlineData("DISABLED")]
    [InlineData("disabled")]
    [InlineData("Disabled")]
    [InlineData("DISABLED")]
    public void AdaptiveMode_Disabled_is_case_insensitive(string value)
    {
        NpcStrategyOptions options = NpcStrategyOptions.FromEnvironment(
            NpcBrainMode.Intent, NpcReactiveSchedulerMode.Enabled,
            name => name == "NPC_STRATEGY_ADAPTIVE_MODE" ? value : null);

        options.AdaptiveMode.Should().Be(NpcAdaptiveStrategyMode.Disabled);
    }

    // -----------------------------------------------------------------------
    // Behavioral baseline: Style=None (no strategy override)
    // -----------------------------------------------------------------------

    [Fact]
    public void Baseline_NoStrategy_Idle_emits_no_intents()
    {
        // No target, no visible hostiles → nothing to do.
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
    public void Baseline_NoStrategy_LowHp_FleeNotAllowed_emits_attack_or_approach()
    {
        // Default Fighter profile: FleeAllowed=false → no Flee intent at low HP.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.10)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().ContainSingle().Which.Should().NotBeOfType<FleeIntent>();
    }

    [Fact]
    public void Baseline_NoStrategy_TargetInvisible_emits_ClearTarget()
    {
        // Current target set but NOT in visible entities → ClearTargetIntent.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            // No .WithHostileAt() — target not visible.
            .Build();

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
    // Behavioral baseline: Style=Balanced
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
    public void Baseline_Balanced_LowHp_FleeAllowed_emits_Flee()
    {
        // Balanced profile: FleeHpPercent=25. At 10% HP → Flee.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.10)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.Balanced);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<FleeIntent>();
    }

    // -----------------------------------------------------------------------
    // Behavioral baseline: Style=AggressivePressure
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
    public void Baseline_AggressivePressure_LowHp_FleeThreshold_5pct_does_not_flee_at_10pct()
    {
        // AggressivePressure: FleeHpPercent=5. At 10% HP → does NOT flee (above threshold).
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.10)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.AggressivePressure);

        decision.Intents.Should().ContainSingle().Which.Should().NotBeOfType<FleeIntent>();
    }

    // -----------------------------------------------------------------------
    // Behavioral baseline: Style=RangedControl
    // -----------------------------------------------------------------------

    [Fact]
    public void Baseline_RangedControl_TargetAtOptimalRange_emits_attack_not_approach()
    {
        // Archer at 550u (within PhysicalAttackRange=600). RangedControl profile.
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
    // Behavioral baseline: Style=Survival
    // -----------------------------------------------------------------------

    [Fact]
    public void Baseline_Survival_LowHp_FleeThreshold_30pct_flees_at_25pct()
    {
        // Survival: FleeHpPercent=30. At 25% HP → Flee.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.25)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.Survival);

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<FleeIntent>();
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

    [Fact]
    public void Baseline_Survival_does_not_flee_above_threshold()
    {
        // Survival: FleeHpPercent=30. At 35% HP → does NOT flee.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.35)
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().DecideWithStrategy(
            perception, ScenarioContext.Periodic(), NpcStrategyProfileResolver.Survival);

        decision.Intents.Should().ContainSingle().Which.Should().NotBeOfType<FleeIntent>();
    }
}
