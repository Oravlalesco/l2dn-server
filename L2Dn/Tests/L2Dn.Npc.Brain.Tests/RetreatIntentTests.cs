using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// NPC-4B5-03 - Tests de MaintainRange + Retreat como Tactical Candidates (4B5-A20).
///
/// Tests de nivel 1: evaluador (score.Action).
/// Tests de nivel 2: TacticalBrain -> RetreatIntent (mapeo end-to-end sin Gateway).
/// Tests de nivel 3: Gateway geo-blocked (A21) -- en NpcIntentGatewayTests.
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

    // Helper: evaluador directo con scores inyectados (nivel 1).
    private static NpcTacticalScore EvalWithHint(NpcPerceptionSnapshot perception, double distance,
        int retreatRange, int maintainRangeScore, int retreatScore) =>
        new TacticalActionEvaluator().Evaluate(perception, MeleeProfile, distance, 0,
            retreatRange, maintainRangeScore, retreatScore);

    // Helper: TacticalBrain.Decide con seam (nivel 2).
    private static NpcIntent? BrainDecideWithHint(NpcPerceptionSnapshot perception,
        int retreatRange, int maintainRangeScore, int retreatScore)
    {
        NpcBrainState state = new(perception.Envelope.Npc);
        return new TacticalBrain().Decide(perception, MeleeProfile, state, decisionSequence: 1,
            retreatRange, maintainRangeScore, retreatScore);
    }

    // -----------------------------------------------------------------------
    // Nivel 1 -- Evaluador: score.Action correcto por zona de banda
    // -----------------------------------------------------------------------

    [Fact]
    public void MaintainRange_TooClose_emits_MaintainRange_score()
    {
        // distance=100, retreatRange=300, tolerance=60 => 100 < 240 => MaintainRange
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

    [Fact]
    public void MaintainRange_TooFar_emits_Approach_score()
    {
        // distance=500, retreatRange=300, tolerance=60 => 500 > 360 => Approach
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

    [Fact]
    public void MaintainRange_InBand_emits_no_range_movement()
    {
        // distance=300, retreatRange=300, tolerance=60 => en banda [240, 360] => sin candidato de rango
        // Nota: el evaluador selecciona Approach fisico (300 > melee reach ~40) -- correcto.
        // MaintainRange solo suprime el movimiento de rango, no el fisico.
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
    }

    [Fact]
    public void Retreat_always_emits_Retreat_score_regardless_of_distance()
    {
        // retreatScore=90 gana a cualquier otro candidato en cualquier distancia
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

    // -----------------------------------------------------------------------
    // Nivel 2 -- TacticalBrain: conversión MaintainRange/Retreat -> RetreatIntent
    // Cubre el hueco de cobertura: los tests de nivel 1 solo verifican score.Action,
    // no el intent final. Estos tests verifican que TacticalBrain mapea correctamente
    // a RetreatIntent (con el Threat y Distance correctos).
    // -----------------------------------------------------------------------

    [Fact]
    public void TacticalBrain_MaintainRange_TooClose_maps_to_RetreatIntent()
    {
        // distance=100 < 240 => MaintainRange => TacticalBrain => RetreatIntent(Threat=Player, Distance=300)
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 100)
            .Build();

        NpcIntent? intent = BrainDecideWithHint(perception,
            retreatRange: 300, maintainRangeScore: 85, retreatScore: 0);

        intent.Should().BeOfType<RetreatIntent>(
            because: "TacticalBrain must map MaintainRange action to RetreatIntent");
        RetreatIntent retreat = (RetreatIntent)intent!;
        retreat.Threat.Should().Be(Player,
            because: "threat is the current combat target");
        retreat.Distance.Should().Be(300,
            because: "desired distance equals the configured retreat range");
    }

    [Fact]
    public void TacticalBrain_PureRetreat_maps_to_RetreatIntent()
    {
        // retreatScore=90 => Retreat action => TacticalBrain => RetreatIntent(Threat=Player, Distance=300)
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 50)
            .Build();

        NpcIntent? intent = BrainDecideWithHint(perception,
            retreatRange: 300, maintainRangeScore: 0, retreatScore: 90);

        intent.Should().BeOfType<RetreatIntent>(
            because: "TacticalBrain must map pure Retreat action to RetreatIntent");
        RetreatIntent retreat = (RetreatIntent)intent!;
        retreat.Threat.Should().Be(Player);
        retreat.Distance.Should().Be(300);
    }
}
