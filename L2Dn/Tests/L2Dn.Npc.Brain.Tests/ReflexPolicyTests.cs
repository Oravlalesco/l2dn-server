using FluentAssertions;
using L2Dn.NpcBrain;
using L2Dn.NpcContracts;

namespace L2Dn.Npc.Brain.Tests;

/// <summary>
/// NPC-4B5-02 — Tests de NpcReflexPolicy (4B5-A16 / A16b).
///
/// Verifica que ReflexBrain consume NpcReflexPolicy inyectada en NpcBrainContext
/// en lugar del fleeHpPercent derivado de strategy.EffectiveFleeHpPercent.
///
/// Criterios:
///   4B5-A16b: AggressivePressure -> EmergencyFleeHpPercent=5%, Survival -> 30% (backward-compat Phase 4B).
///   4B5-A16 : ReflexBrain usa SOLO NpcReflexPolicy; nunca lee strategy durante Think.
/// </summary>
public class ReflexPolicyTests
{
    private static readonly EntityKey Player = new(9001, 0, EntityKind.Player);

    // -----------------------------------------------------------------------
    // Helper: construye un NpcBrainContext con FleeAllowed=true e inyecta
    // NpcReflexPolicy directamente, simulando lo que ResolveReflexPolicy produce
    // en BrainNpcThinkExecutor (sin necesitar el host de GameServer).
    // -----------------------------------------------------------------------
    private static NpcBrainContext PolicyContext(double fleeHpPercent, int leashRange = 200) =>
        new(NpcBrainStimulus.Attacked,
            new NpcIntelligenceProfile(
                NpcIntelligenceArchetype.BasicMeleeMob,
                ReflexEnabled: true, TacticalEnabled: true,
                AcquireVisibleHostiles: true, FleeAllowed: false, // FleeAllowed en profile=false
                FleeHpPercent: 99,   // irrelevante: la policy toma precedencia
                PreferredRange: 0, LeashDistance: leashRange),
            ReflexPolicy: new NpcReflexPolicy(
                EmergencyFleeHpPercent: fleeHpPercent,
                FleeAllowed: true,          // policy dice "si puede huir"
                LeashRange: leashRange));

    // -----------------------------------------------------------------------
    // 4B5-A16b: AggressivePressure preserva FleeHpPercentOverride=5% de Phase 4B.
    // NPC at 3% HP con policy de 5% -> debe emitir FleeIntent.
    // -----------------------------------------------------------------------
    [Fact]
    public void ReflexPolicy_AggressivePressure_FleeFivePercent()
    {
        // AggressivePressure: FleeHpPercentOverride=5 -> ResolveReflexPolicy -> EmergencyFleeHpPercent=5.
        // Aqui lo inyectamos directamente (5%) para testear el Brain en aislamiento.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.03)  // 3% HP < 5% threshold -> debe huir
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator()
            .Decide(perception, PolicyContext(fleeHpPercent: 5));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<FleeIntent>();
    }

    // -----------------------------------------------------------------------
    // 4B5-A16b: Survival preserva FleeHpPercentOverride=30% de Phase 4B.
    // NPC at 25% HP con policy de 30% -> debe emitir FleeIntent.
    // -----------------------------------------------------------------------
    [Fact]
    public void ReflexPolicy_Survival_FleeThirtyPercent()
    {
        // Survival: FleeHpPercentOverride=30 -> ResolveReflexPolicy -> EmergencyFleeHpPercent=30.
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.25)  // 25% HP < 30% threshold -> debe huir
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        NpcBrainDecision decision = new NpcBrainCoordinator()
            .Decide(perception, PolicyContext(fleeHpPercent: 30));

        decision.Intents.Should().ContainSingle().Which.Should().BeOfType<FleeIntent>();
    }

    // -----------------------------------------------------------------------
    // 4B5-A16: ReflexBrain usa SOLO NpcReflexPolicy, no strategy.EffectiveFleeHpPercent.
    //
    // Setup:
    //   - NpcReflexPolicy.EmergencyFleeHpPercent = 5%  (policy dice "flee si HP < 5%")
    //   - strategy (Survival) diria EffectiveFleeHpPercent = 30%
    //   - HP del NPC = 10%
    //
    // Si Brain usa la policy (5%) -> 10% > 5% -> NO huye  <- correcto
    // Si Brain usara strategy (30%) -> 10% < 30% -> huiria <- incorrecto (regresion)
    // -----------------------------------------------------------------------
    [Fact]
    public void ReflexFlee_UsesOnlyNpcReflexPolicy()
    {
        NpcPerceptionSnapshot perception = NpcScenarioBuilder.CreateMelee()
            .WithHp(0.10)  // 10% HP
            .WithCurrentTarget(Player)
            .WithHostileAt(Player, distance2D: 300)
            .Build();

        // policy dice 5%; si el Brain consultara Survival (30%) huiria erróneamente.
        NpcBrainDecision decision = new NpcBrainCoordinator()
            .DecideWithStrategy(perception, PolicyContext(fleeHpPercent: 5),
                NpcStrategyProfileResolver.Survival);  // Survival.FleeHpPercentOverride=30

        // 10% > 5% -> la policy NO activa flee -> NO debe haber FleeIntent.
        decision.Intents.Should().NotContain(i => i is FleeIntent,
            because: "ReflexBrain debe usar NpcReflexPolicy(5%) y no strategy.EffectiveFleeHpPercent(30%)");
    }
}
