using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// NPC-4B5-03 - Tests de MaintainRange + Retreat como Tactical Candidates (4B5-A20).
///
/// Los tests invocan el overload internal de TacticalActionEvaluator con scores inyectados
/// directamente, aislando el Brain del host GameServer.
/// En produccion (sin hint), estos candidates tienen score=0 y nunca compiten (baseline 4B.5.0).
/// </summary>
public class RetreatIntentTests
{
    private static readonly EntityKey Player = new(9001, 0, EntityKind.Player);

    // NpcIntelligenceProfile basico (Melee) con TacticalEnabled=true y FleeAllowed=false.
    // FleeAllowed=false: aisla flee path del Reflex; solo probamos el camino Tactical.
    private static readonly NpcIntelligenceProfile MeleeProfile = new(
        NpcIntelligenceArchetype.BasicMeleeMob,
        ReflexEnabled: true, TacticalEnabled: true,
        AcquireVisibleHostiles: true, FleeAllowed: false,
        FleeHpPercent: 0, PreferredRange: 0, LeashDistance: 200);

    // Helper: evaluador directo con scores inyectados.
    private static NpcTacticalScore EvalWithHint(NpcPerceptionSnapshot perception, double distance,
        int retreatRange, int maintainRangeScore, int retreatScore) =>
        new TacticalActionEvaluator().Evaluate(perception, MeleeProfile, distance, 0,
            retreatRange, maintainRangeScore, retreatScore);

    // -----------------------------------------------------------------------
    // 4B5-A20: MaintainRange con target DEMASIADO CERCA -> RetreatIntent
    // distance=100, retreatRange=300, tolerance=60 => 100 < 240 => MaintainRange
    // -----------------------------------------------------------------------
    [Fact]
    public void MaintainRange_TooClose_emits_RetreatIntent()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 100)
            .Build();

        NpcTacticalScore score = EvalWithHint(perception, distance: 100,
            retreatRange: 300, maintainRangeScore: 85, retreatScore: 0);

        score.Action.Should().Be(NpcTacticalAction.MaintainRange,
            because: "target at 100 is below retreatRange(300) - tolerance(60) = 240");
        score.DesiredRange.Should().Be(300);
    }

    // -----------------------------------------------------------------------
    // 4B5-A20: MaintainRange con target DEMASIADO LEJOS -> Approach
    // distance=500, retreatRange=300, tolerance=60 => 500 > 360 => Approach
    // -----------------------------------------------------------------------
    [Fact]
    public void MaintainRange_TooFar_emits_Approach()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 500)
            .Build();

        NpcTacticalScore score = EvalWithHint(perception, distance: 500,
            retreatRange: 300, maintainRangeScore: 85, retreatScore: 0);

        score.Action.Should().Be(NpcTacticalAction.Approach,
            because: "target at 500 is above retreatRange(300) + tolerance(60) = 360");
        score.DesiredRange.Should().Be(300,
            because: "approach should close to preferred range, not physical attack range");
    }

    // -----------------------------------------------------------------------
    // 4B5-A20: MaintainRange con target EN BANDA [240, 360] -> sin movimiento de rango
    // distance=300, retreatRange=300, tolerance=60 => 300 en [240, 360] => in band
    //
    // Nota: el evaluador selecciona Approach fisico (300 > melee reach ~40), lo cual
    // es correcto. MaintainRange solo suprime el movimiento de rango, no el fisico.
    // -----------------------------------------------------------------------
    [Fact]
    public void MaintainRange_InBand_emits_no_range_movement()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcTacticalScore score = EvalWithHint(perception, distance: 300,
            retreatRange: 300, maintainRangeScore: 85, retreatScore: 0);

        score.Action.Should().NotBe(NpcTacticalAction.MaintainRange,
            because: "target at 300 is within tolerance band [240, 360], no range-retreat needed");
        score.Action.Should().NotBe(NpcTacticalAction.Retreat,
            because: "retreatScore=0 so pure Retreat is also not selected");
        // The evaluator correctly selects physical Approach (target at 300 > melee reach ~40).
        // MaintainRange suppresses *range management* movement, not physical approach.
    }

    // -----------------------------------------------------------------------
    // 4B5-A20: Retreat puro SIEMPRE emite RetreatIntent, independiente de la distancia
    // -----------------------------------------------------------------------
    [Fact]
    public void Retreat_always_emits_Retreat_regardless_of_distance()
    {
        // NPC muy cerca (50 unidades) -- Retreat debe ganar si su score es mayor
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 50)
            .Build();

        NpcTacticalScore score = EvalWithHint(perception, distance: 50,
            retreatRange: 300, maintainRangeScore: 0, retreatScore: 90);

        score.Action.Should().Be(NpcTacticalAction.Retreat,
            because: "retreatScore=90 is higher than any other action; pure retreat is distance-independent");
        score.DesiredRange.Should().Be(300);
    }
}
