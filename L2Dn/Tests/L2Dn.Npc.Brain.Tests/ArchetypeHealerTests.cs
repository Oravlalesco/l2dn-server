using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// VAL-06 — Healer / Support archetype tests (FORWARD-LOOKING).
///
/// All tests are Skipped because curing/buffing allies requires perception of
/// ally HP states, which is part of Phase 4E (Squad Intelligence).
/// The file is pre-written to establish the API contract for when 4E lands.
/// </summary>
public class ArchetypeHealerTests
{
    private static readonly EntityKey Self  = new(4000, 0, EntityKind.Npc);
    private static readonly EntityKey Ally1 = new(4001, 0, EntityKind.Npc);

    [Fact(Skip = "Requiere 4E — Squad Intelligence (percepción de HP de aliados)")]
    public void Healer_casts_heal_on_low_hp_ally()
    {
        // Expected: CastSkillIntent targeting Ally1 when ally HP < 50%.
        // Requires Phase 4E ally HP observation and squad snapshot.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithAllyAt(Ally1, distance2D: 100)
            .WithSkillReady(id: 1013, NpcSkillCategory.Heal, range: 600,
                flags: NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ActionReady());

        decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<CastSkillIntent>()
            .Which.Target.Should().Be(Ally1);
    }

    [Fact(Skip = "Requiere 4E — Squad Intelligence (cura a aliados)")]
    public void Healer_prioritizes_critical_ally_over_buffing()
    {
        // Expected: Heal (Hp < 30%) takes priority over Buff when both skills are ready.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithAllyAt(Ally1, distance2D: 100)
            .WithSkillReady(id: 1013, NpcSkillCategory.Heal, range: 600,
                flags: NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic)
            .WithSkillReady(id: 1014, NpcSkillCategory.Buff, range: 600,
                flags: NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.ActionReady());

        CastSkillIntent castIntent = decision.Intents.Should().ContainSingle()
            .Which.Should().BeOfType<CastSkillIntent>().Subject;
        castIntent.SkillId.Should().Be(1013); // heal, not buff
    }

    [Fact(Skip = "Requiere 4E — Squad Intelligence (no abandonar a aliados heridos)")]
    public void Healer_does_not_flee_while_ally_is_critically_injured()
    {
        // Even at low HP, healer should prioritize healing a critical ally over fleeing.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.18) // Below typical flee threshold
            .WithAllyAt(Ally1, distance2D: 80)
            .WithSkillReady(id: 1013, NpcSkillCategory.Heal, range: 600,
                flags: NpcSkillObservationFlags.Ready | NpcSkillObservationFlags.Magic)
            .Build();

        NpcBrainDecision decision =
            new NpcBrainCoordinator().Decide(perception, ScenarioContext.Periodic());

        decision.Intents.Should().NotContain(i => i is FleeIntent);
    }
}
