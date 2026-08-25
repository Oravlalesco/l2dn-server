using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// NPC-4B5-05 (4B.5.4) -- Tests for NpcStrategyEvaluationContext + StrategyContextBuilder.
/// All tests use NpcScenarioBuilder; no I/O; deterministic.
/// </summary>
public class StrategyContextBuilderTests
{
    private static readonly EntityKey Player = new(9001, 0, EntityKind.Player);

    private static readonly NpcIntelligenceProfile MeleeProfile = new(
        NpcIntelligenceArchetype.BasicMeleeMob,
        ReflexEnabled: true, TacticalEnabled: true,
        AcquireVisibleHostiles: true, FleeAllowed: false,
        FleeHpPercent: 0, PreferredRange: 0, LeashDistance: 200);

    private static readonly NpcIntelligenceProfile RangedProfile = new(
        NpcIntelligenceArchetype.BasicCasterMob,
        ReflexEnabled: true, TacticalEnabled: true,
        AcquireVisibleHostiles: true, FleeAllowed: true,
        FleeHpPercent: 20, PreferredRange: 500, LeashDistance: 200);

    // -----------------------------------------------------------------------
    // HP ratio derivation: HpPercent (0-100) -> SelfHpRatio (0-1000)
    // -----------------------------------------------------------------------
    [Fact]
    public void StrategyContext_Builder_DerivesHpRatio_0to1000()
    {
        // WithHp(0.35) sets CurrentHp = MaxHp * 0.35; HpPercent = 35.0; *10 = 350
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.35)
            .Build();

        NpcStrategyEvaluationContext ctx = StrategyContextBuilder.Build(perception, MeleeProfile);

        ctx.SelfHpRatio.Should().Be(350,
            because: "HpPercent(35%) * 10 = 350 in 0-1000 scale");
        ctx.SelfHpRatio.Should().BeInRange(0, 1000,
            because: "ratio must stay within [0, 1000]");
    }

    // -----------------------------------------------------------------------
    // Target distance from VisibleEntity.Distance2D
    // -----------------------------------------------------------------------
    [Fact]
    public void StrategyContext_Builder_DerivesTargetDistance()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 420)
            .Build();

        NpcStrategyEvaluationContext ctx = StrategyContextBuilder.Build(perception, MeleeProfile);

        ctx.HasTarget.Should().BeTrue();
        ctx.TargetDistance.Should().Be(420,
            because: "VisibleEntity.Distance2D=420 is cast to int");
    }

    // -----------------------------------------------------------------------
    // Proximity bands: TargetWithinRange / TargetClose / TargetFar
    // preferredRange=500, tolerance=60 => band [440, 560]
    // TargetClose: distance < 250 (preferred/2)
    // TargetFar:   distance > 750 (preferred*3/2)
    // -----------------------------------------------------------------------
    [Theory]
    [InlineData(200, false, true,  false)] // 200 < 440 -> below band, close (200 < 250)
    [InlineData(500, true,  false, false)] // 500 in [440, 560] -> within range
    [InlineData(900, false, false, true)]  // 900 > 560 -> above band, far (900 > 750)
    public void StrategyContext_Builder_ComputesWithinCloseFar(
        int distance, bool expectWithin, bool expectClose, bool expectFar)
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: distance)
            .Build();

        NpcStrategyEvaluationContext ctx = StrategyContextBuilder.Build(perception, RangedProfile);

        ctx.TargetWithinRange.Should().Be(expectWithin,
            because: $"distance={distance} vs band [440, 560]");
        ctx.TargetClose.Should().Be(expectClose,
            because: $"distance={distance} vs close threshold=250");
        ctx.TargetFar.Should().Be(expectFar,
            because: $"distance={distance} vs far threshold=750");
    }

    // -----------------------------------------------------------------------
    // Zero-alloc: Build() must not allocate on the managed heap after JIT warmup.
    // Warmup call discards JIT allocs; only the measured call is checked.
    // -----------------------------------------------------------------------
    [Fact]
    public void StrategyContext_Builder_NoHeapAllocation()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        // Warmup: saturate JIT + any lazy-initialized static fields (comparers, delegates).
        for (int i = 0; i < 10; i++)
            _ = StrategyContextBuilder.Build(perception, MeleeProfile);

        // Measure 10 consecutive calls: if Build() allocates, 10 calls multiply the evidence.
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10; i++)
            _ = StrategyContextBuilder.Build(perception, MeleeProfile);
        long after = GC.GetAllocatedBytesForCurrentThread();

        (after - before).Should().Be(0,
            because: "StrategyContextBuilder.Build must not allocate heap memory after JIT warmup");
    }

    // -----------------------------------------------------------------------
    // Default posture when no state is provided
    // -----------------------------------------------------------------------
    [Fact]
    public void StrategyContext_Builder_DefaultsPostureNeutral()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee().Build();

        NpcStrategyEvaluationContext ctx = StrategyContextBuilder.Build(perception, MeleeProfile);

        ctx.CurrentPosture.Should().Be(NpcStrategicPosture.Neutral,
            because: "without explicit state, posture defaults to Neutral (outside combat)");
        ctx.TimeInPostureTicks.Should().Be(0,
            because: "without explicit state, time defaults to 0");
        ctx.CombatDurationTicks.Should().Be(0,
            because: "without explicit state, combat duration defaults to 0");
    }
}
