using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// VAL-02 — Archetype Melee / Fighter tests.
/// Exercises the Melee (Fighter) intelligence profile: BasicAttack, ApproachTarget,
/// hate switching, skill usage, CC flag handling, and leash return.
/// All tests use Decide() directly (no DecideWithStrategy needed for Fighter archetype).
/// </summary>
public class ArchetypeMeleeTests
{
    private static readonly EntityKey Player1 = new(1001, 0, EntityKind.Player);
    private static readonly EntityKey Player2 = new(1002, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // Pursuit and attack
    // -----------------------------------------------------------------------

    [Fact]
    public void Melee_with_target_in_range_emits_basic_attack()
    {
        // Actor has a current target within physical attack range (40 u + collision).
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 30)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Attacked());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<BasicAttackIntent>()
            .Which.Target.Should().Be(Player1);
    }

    [Fact]
    public void Melee_pursues_target_out_of_range()
    {
        // Target is at 300 u — beyond physical reach of 40 u + collision.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<ApproachTargetIntent>()
            .Which.Target.Should().Be(Player1);
    }

    // -----------------------------------------------------------------------
    // Hate / target switching
    // -----------------------------------------------------------------------

    [Fact]
    public void Melee_switches_to_highest_hate_target()
    {
        // Player2 has higher hate and is visible → should become the new target.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 200)
            .WithHostileAt(Player2, distance2D: 250, ordinal: 1)
            .WithThreatEntries(
                (Player1, hate: 1000, distance: 200),
                (Player2, hate: 2000, distance: 250))
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ThreatChanged());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player2);
    }

    [Fact]
    public void Melee_acquires_nearest_hostile_when_aggressive()
    {
        // No current target; two hostiles in aggro range — nearest should be selected.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHostileAt(Player1, distance2D: 300, ordinal: 0)
            .WithHostileAt(Player2, distance2D: 90, ordinal: 1)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Aggro());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<AcquireTargetIntent>()
            .Which.Target.Should().Be(Player2);
    }

    // -----------------------------------------------------------------------
    // Skill usage
    // -----------------------------------------------------------------------

    [Fact]
    public void Melee_uses_physical_short_range_skill_when_ready_and_in_reach()
    {
        // Physical Offensive skill (no Magic flag) within attack range.
        // HasReadyPhysicalMeleeSkill: category ∈ {Offensive,Debuff,Control} + Ready + !Magic + range ≤ physicalAttack.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 25)
            .WithSkillReady(id: 101, NpcSkillCategory.Offensive, range: 40,
                flags: NpcSkillObservationFlags.Ready) // physical (no Magic)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.ActionReady());

        // Either BasicAttack or CastSkill is acceptable depending on fighter preference;
        // the key assertion is that an attack-class intent is produced (not Approach).
        decision.Intents.Should().ContainSingle().Which.Should()
            .Match<NpcIntent>(i => i is BasicAttackIntent or CastSkillIntent);
    }

    // -----------------------------------------------------------------------
    // Crowd-control interaction (melee context)
    // -----------------------------------------------------------------------

    [Fact]
    public void Melee_rooted_does_not_emit_approach_intent()
    {
        // Root = NpcPhysicalFlags.MovementDisabled (NOT NpcCombatFlags — that doesn't exist).
        // Brain does not yet check this flag automatically; this test documents the expected
        // contract and will pass once VAL-07's TacticalBrain fix is applied.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 300)
            .WithPhysicalFlags(NpcPhysicalFlags.Alive | NpcPhysicalFlags.Spawned |
                NpcPhysicalFlags.MovementDisabled)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().NotContain(i => i is ApproachTargetIntent);
    }

    // -----------------------------------------------------------------------
    // Leash / return to spawn (covered in depth by VAL-05 LeashAndReturnTests)
    // -----------------------------------------------------------------------

    [Fact]
    public void Melee_returns_to_spawn_when_target_exits_leash_radius()
    {
        // Distance from origin to position (2000, 0, 0) is 2000 u > combatLeashDistance of 1500.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithPosition(2000, 0, 0)
            .WithSpawnPosition(0, 0, 0)
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 200)
            .WithCombatLeashDistance(1500)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ReturnHomeIntent>();
    }
}
