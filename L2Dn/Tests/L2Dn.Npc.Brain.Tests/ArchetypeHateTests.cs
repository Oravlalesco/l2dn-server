using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// VAL-04 — Hate / multi-target dispute tests.
///
/// Key behavior of SelectHighestVisibleThreat (verified in NpcPerceptionFacts.cs):
///   - Uses STRICTLY GREATER than (>), so first-observed entry wins on ties.
///   - Stability on equal hate = first-wins semantics, NOT a hysteresis threshold.
///   - Requires: Visible=true, ValidTarget=true, Hate > 0.
/// </summary>
public class ArchetypeHateTests
{
    private static readonly EntityKey Tank   = new(2001, 0, EntityKind.Player);
    private static readonly EntityKey Mage   = new(2002, 0, EntityKind.Player);
    private static readonly EntityKey Archer = new(2003, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // Basic hate selection
    // -----------------------------------------------------------------------

    [Fact]
    public void Npc_acquires_highest_hate_target_from_threat_table()
    {
        // Tank 1 000 hate, Mage 1 200 hate → Mage should be selected.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHostileAt(Tank, distance2D: 150, ordinal: 0)
            .WithHostileAt(Mage, distance2D: 800, ordinal: 1)
            .WithThreatEntry(Tank, hate: 1000, damage: 500, distance: 150)
            .WithThreatEntry(Mage, hate: 1200, damage: 600, distance: 800)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ThreatChanged());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Mage);
    }

    [Fact]
    public void Npc_keeps_first_observed_target_on_equal_hate()
    {
        // Equal hate: Tank is processed first in the collection → Tank wins (first-wins semantics).
        // The selector uses `>` (strictly greater), so equal hate never displaces the current winner.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHostileAt(Tank, distance2D: 150, ordinal: 0)
            .WithHostileAt(Mage, distance2D: 200, ordinal: 1)
            .WithThreatEntry(Tank, hate: 1000, damage: 500, distance: 150)
            .WithThreatEntry(Mage, hate: 1000, damage: 500, distance: 200)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ThreatChanged());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Tank);
    }

    [Fact]
    public void Npc_switches_to_higher_hate_after_significant_gap()
    {
        // Start with Tank as current target (hate 1 200).
        // Mage generates new hate surge to 3 600 (3×) — strictly greater, so switch.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Tank)
            .WithHostileAt(Tank, distance2D: 150, ordinal: 0)
            .WithHostileAt(Mage, distance2D: 800, ordinal: 1)
            .WithThreatEntry(Tank, hate: 1200, damage: 600, distance: 150)
            .WithThreatEntry(Mage, hate: 3600, damage: 1800, distance: 800)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ThreatChanged());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Mage);
    }

    // -----------------------------------------------------------------------
    // Invalid / invisible threat entries
    // -----------------------------------------------------------------------

    [Fact]
    public void Npc_ignores_threat_entry_with_validTarget_false()
    {
        // ValidTarget = false causes SelectHighestVisibleThreat to skip the entry.
        // The NPC should NOT acquire Mage even though its hate is higher.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHostileAt(Tank, distance2D: 150, ordinal: 0)
            .WithThreatEntry(Tank,  hate: 500,  damage: 250, distance: 150, visible: true, validTarget: true)
            .WithThreatEntry(Mage,  hate: 5000, damage: 2500, distance: 800, visible: true, validTarget: false)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ThreatChanged());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Tank);
    }

    [Fact]
    public void Npc_ignores_invisible_threat_entry()
    {
        // Visible = false → SelectHighestVisibleThreat ignores the entry.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHostileAt(Tank, distance2D: 150, ordinal: 0)
            .WithThreatEntry(Tank,   hate: 500,  damage: 250, distance: 150, visible: true,  validTarget: true)
            .WithThreatEntry(Archer, hate: 9000, damage: 4500, distance: 400, visible: false, validTarget: true)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ThreatChanged());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Tank);
    }

    [Fact]
    public void Npc_skips_zero_hate_entry_in_threat_table()
    {
        // Hate = 0 does not satisfy `entry.Hate > highestHate` (highestHate starts at 0).
        // No valid hate entry → no threat-based target selection.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithThreatEntry(Tank, hate: 0, damage: 0, distance: 150)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ThreatChanged());

        // No target acquired via threat table; passive (no Aggressive cap needed here since
        // there are no visible hostiles to auto-acquire either).
        decision.Intents.OfType<AcquireTargetIntent>().Should().NotContain(a => a.Target == Tank);
    }
}
