using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// VAL-05 — Leash and return-to-spawn tests.
/// Exercises ReflexBrain's leash / return state machine verified against the
/// actual ReflexBrain.cs implementation.
/// </summary>
public class LeashAndReturnTests
{
    private static readonly EntityKey Player1 = new(3001, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // Leash triggers
    // -----------------------------------------------------------------------

    [Fact]
    public void Npc_returns_home_when_outside_combat_leash_radius()
    {
        // Actor at (2000, 0, 0); spawn at (0, 0, 0). Distance = 2000 > leash 1500.
        // ReflexBrain.outsideCombatLeash → triggers return flow.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithPosition(2000, 0, 0)
            .WithSpawnPosition(0, 0, 0)
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 100)
            .WithThreatEntry(Player1, hate: 1000, damage: 500, distance: 100)
            .WithCombatLeashDistance(1500)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<ReturnHomeIntent>();
    }

    [Fact]
    public void Npc_does_not_return_home_while_inside_combat_leash_radius()
    {
        // Actor at (500, 0, 0); spawn at (0, 0, 0). Distance = 500 < leash 1500.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithPosition(500, 0, 0)
            .WithSpawnPosition(0, 0, 0)
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 50)
            .WithCombatLeashDistance(1500)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().NotContain(i => i is ReturnHomeIntent);
    }

    // -----------------------------------------------------------------------
    // Dead / unspawned actor
    // -----------------------------------------------------------------------

    [Fact]
    public void Dead_npc_produces_no_intents()
    {
        // IsActorOperational() requires Alive + Spawned. Without Alive, 0 intents.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithPhysicalFlags(NpcPhysicalFlags.Spawned) // Alive intentionally omitted
            .WithCurrentTarget(Player1)
            .WithHostileAt(Player1, distance2D: 50)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    // ReturningToSpawn state
    // -----------------------------------------------------------------------

    [Fact]
    public void Npc_returning_to_spawn_does_not_acquire_new_hostile()
    {
        // returningToSpawn = true and actor is not at spawn yet (at 500 u from spawn).
        // ReflexBrain: ReturningToSpawn path — visibility events alone do not interrupt a return.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithPosition(500, 0, 0)
            .WithSpawnPosition(0, 0, 0)
            .ReturningToSpawn()
            .WithHostileAt(Player1, distance2D: 80)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().NotContain(i => i is AcquireTargetIntent);
    }

    [Fact]
    public void Npc_at_spawn_position_does_not_emit_redundant_return_home()
    {
        // Actor is at (0, 0, 0) = spawn. distanceFromSpawn = 0 ≤ returnHomeDistance.
        // No outsideHomeRange → no ReturnHomeIntent emitted.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithPosition(0, 0, 0)
            .WithSpawnPosition(0, 0, 0)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().NotContain(i => i is ReturnHomeIntent);
    }

    // -----------------------------------------------------------------------
    // Passive actor
    // -----------------------------------------------------------------------

    [Fact]
    public void Passive_npc_does_not_auto_acquire_hostile_without_threat()
    {
        // No Aggressive capability → AcquireVisibleHostiles = false in intelligence profile.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreatePassive()
            .WithHostileAt(Player1, distance2D: 90)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Aggro());

        decision.Intents.Should().BeEmpty();
    }
}
