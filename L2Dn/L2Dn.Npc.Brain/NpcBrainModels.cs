using System.Collections.Immutable;
using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

[Flags]
public enum NpcBrainStimulus
{
    None = 0,
    PeriodicDue = 1 << 0,
    PlayerBecameRelevant = 1 << 1,
    Attacked = 1 << 2,
    ThreatChanged = 1 << 3,
    TargetLost = 1 << 4,
    TargetDied = 1 << 5,
    AllyAttacked = 1 << 6,
    CombatStarted = 1 << 7,
    CombatEnded = 1 << 8,
    RegionActivated = 1 << 9,
    Respawned = 1 << 10,
    ActionReady = 1 << 11
}

public enum NpcBrainLayer
{
    None = 0,
    Reflex = 1,
    Tactical = 2
}

public enum NpcIntelligenceArchetype
{
    BasicPassiveMob = 1,
    BasicAggressiveMob = 2,
    BasicCasterMob = 3,
    BasicMeleeMob = 4
}

public sealed record NpcIntelligenceProfile(
    NpcIntelligenceArchetype Archetype,
    bool ReflexEnabled,
    bool TacticalEnabled,
    bool AcquireVisibleHostiles,
    bool FleeAllowed,
    double FleeHpPercent,
    int PreferredRange,
    int LeashDistance);

public sealed record NpcReturnDefensePolicy(
    bool Enabled,
    int TimeoutWorldTicks,
    int LeashGraceWorldTicks = 200,
    int MaxLeashExcursions = 3,
    int HardLeashExtension = 500)
{
    public static NpcReturnDefensePolicy Default { get; } = new(true, 1200, 200, 3, 500);
}

public sealed record NpcBrainContext(
    NpcBrainStimulus Stimuli,
    NpcIntelligenceProfile? Profile = null,
    NpcReturnDefensePolicy? ReturnDefense = null);

public sealed record NpcBrainDecision
{
    public NpcBrainDecision(NpcKey actor, long decisionSequence, NpcBrainLayer layer,
        ImmutableArray<NpcIntent> intents, NpcStrategyDecisionSummary? strategyDecision = null)
    {
        Actor = actor;
        DecisionSequence = decisionSequence;
        Layer = layer;
        Intents = intents.IsDefault ? [] : intents;
        StrategyDecision = strategyDecision;
    }

    public NpcKey Actor { get; }
    public long DecisionSequence { get; }
    public NpcBrainLayer Layer { get; }
    public ImmutableArray<NpcIntent> Intents { get; }
    public NpcStrategyDecisionSummary? StrategyDecision { get; }
    public NpcStrategyArchetype? Strategy => StrategyDecision?.Profile;
}

public interface INpcBrain
{
    NpcBrainDecision Decide(NpcPerceptionSnapshot perception, NpcBrainContext context);
    void Remove(NpcKey npc);
}
