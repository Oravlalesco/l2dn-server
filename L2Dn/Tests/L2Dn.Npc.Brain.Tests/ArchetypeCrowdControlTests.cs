using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// VAL-07 — Crowd Control / debuff response tests.
///
/// Flag mapping verified against the code:
///   Root    → NpcPhysicalFlags.MovementDisabled
///   Silence → NpcCombatFlags.AllSkillsDisabled   (makes actor non-operational in IsActorOperational)
///   Stun    → MovementDisabled + AllSkillsDisabled
///   Fear    → NpcCombatFlags.Confused             (different from stun)
///
/// IMPORTANT — gap documented for Root (VAL-07 fix):
///   NpcPhysicalFlags.MovementDisabled has 0 usages in L2Dn.Npc.Brain (verified).
///   TacticalBrain.DecideCore does NOT check this flag before emitting ApproachTargetIntent.
///   Tests expecting "no Approach when rooted" WILL FAIL until the fix is applied in TacticalBrain.
///
/// FIX: Add check in TacticalBrain.DecideCore:
///   if (perception.State.Physical.Flags.HasFlag(NpcPhysicalFlags.MovementDisabled))
///       → suppress Approach action (return BasicAttack if in range, else null)
///
/// AllSkillsDisabled passes through IsActorOperational() → actor treated as non-operational → 0 intents.
/// Tests for silence/stun document this as "apagado total", not "selective suppression".
/// </summary>
public class ArchetypeCrowdControlTests
{
    private static readonly EntityKey Player1 = new(5001, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // Root — NpcPhysicalFlags.MovementDisabled
    // -----------------------------------------------------------------------

    [Fact]
    public void Rooted_npc_does_not_emit_approach_intent()
    {
        // Root = NpcPhysicalFlags.MovementDisabled.
        // Expected: TacticalBrain must suppress Approach when rooted.
        // NOTE: This test requires the VAL-07 fix in TacticalBrain (see class doc).
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 300) // beyond melee reach → would normally Approach
            .WithPhysicalFlags(NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned |
                NpcPhysicalFlags.MovementDisabled)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().NotContain(i => i is ApproachTargetIntent);
    }

    [Fact]
    public void Rooted_npc_still_attacks_if_target_is_in_melee_range()
    {
        // Rooted but target is within physical reach — BasicAttack is valid (no movement needed).
        // Root does NOT suppress non-movement intents.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 25) // within physicalAttackRange of 40
            .WithPhysicalFlags(NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned |
                NpcPhysicalFlags.MovementDisabled)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Attacked());

        // Should still attack — root doesn't prevent fighting in place.
        // NOTE: This test depends on the same VAL-07 fix; without it BasicAttack may
        // be the fallback anyway because Approach would normally be chosen for targets
        // in melee reach after the first tick.
        decision.Intents.Should().NotBeEmpty("a rooted NPC can still attack in melee range");
        decision.Intents.Should().NotContain(i => i is ApproachTargetIntent);
    }

    // -----------------------------------------------------------------------
    // Silence / AllSkillsDisabled — makes actor non-operational (0 intents)
    // -----------------------------------------------------------------------

    [Fact]
    public void Silenced_npc_produces_no_intents()
    {
        // AllSkillsDisabled → IsActorOperational() = false → 0 intents (full shutdown).
        // This is "apagado total", not selective skill suppression.
        // Note: a silenced NPC losing all intents (including BasicAttack) is the current
        // behavior of IsActorOperational. Document this rather than assert only CastSkill absence.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 25)
            .WithCombatFlags(NpcCombatFlags.AllSkillsDisabled)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().BeEmpty("AllSkillsDisabled makes the actor non-operational");
    }

    // -----------------------------------------------------------------------
    // Stun — MovementDisabled + AllSkillsDisabled → non-operational (0 intents)
    // -----------------------------------------------------------------------

    [Fact]
    public void Stunned_npc_produces_no_intents()
    {
        // Stun = MovementDisabled (physical) + AllSkillsDisabled (combat).
        // AllSkillsDisabled is sufficient to fail IsActorOperational → 0 intents.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 25)
            .WithPhysicalFlags(NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned |
                NpcPhysicalFlags.MovementDisabled)
            .WithCombatFlags(NpcCombatFlags.AllSkillsDisabled)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().BeEmpty("stunned actor is non-operational");
    }

    // -----------------------------------------------------------------------
    // Recovery — normal behavior restored after CC clears
    // -----------------------------------------------------------------------

    [Fact]
    public void Npc_recovers_normal_behavior_after_cc_clears()
    {
        // Same NPC, same scenario — but NO CC flags. Should produce intents normally.
        NpcPerceptionSnapshot perceptionUnderCc = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 25)
            .WithCombatFlags(NpcCombatFlags.AllSkillsDisabled)
            .Build();

        NpcPerceptionSnapshot perceptionAfterCc = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 25)
            .Build(); // No CC flags — default Alive | Spawned

        NpcBrainCoordinator coordinator = new();
        NpcBrainDecision underCc = coordinator.Decide(perceptionUnderCc, ScenarioContext.Periodic());
        NpcBrainDecision afterCc = coordinator.Decide(perceptionAfterCc, ScenarioContext.Periodic());

        underCc.Intents.Should().BeEmpty("CC makes actor non-operational");
        afterCc.Intents.Should().NotBeEmpty("actor should operate normally after CC cleared");
    }

    // -----------------------------------------------------------------------
    // Confusion / Fear — NpcCombatFlags.Confused (distinct from stun)
    // -----------------------------------------------------------------------

    [Fact]
    public void Confused_npc_current_behavior_is_documented()
    {
        // NpcCombatFlags.Confused = confuse/fear. NOT the same as stun.
        // Unlike AllSkillsDisabled, Confused does NOT make IsActorOperational() return false.
        // This test documents current behavior (Confused alone does NOT suppress intents).
        // Future phases may add explicit handling; for now it's a known gap.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 25)
            .WithCombatFlags(NpcCombatFlags.Confused)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        // Confused alone does NOT trigger the IsActorOperational short-circuit.
        // Document current behavior: the actor remains operational.
        // (If this changes in future phases, update the assertion accordingly.)
        decision.Intents.Should().NotBeEmpty(
            "Confused flag alone does not make actor non-operational in the current implementation");
    }
}
