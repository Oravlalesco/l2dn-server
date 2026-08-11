namespace L2Dn.NpcBrain;

public enum NpcStrategyArchetype
{
    Balanced = 0,
    AggressivePressure = 1,
    RangedControl = 2,
    Survival = 3
}

/// <summary>
/// Immutable, cached policy selected before Reflex/Tactical evaluation. It contains
/// priorities and thresholds only; it cannot execute commands or authorize gameplay.
/// </summary>
public sealed record NpcStrategyProfile(
    string ProfileId,
    NpcStrategyArchetype Archetype,
    int BasicAttackScore,
    int ApproachScore,
    int OffensiveSkillScore,
    int HealScore,
    int FleeScore,
    double HealHpPercent,
    double? FleeHpPercentOverride = null,
    int PreferredRangeOverride = 0);

public readonly record struct NpcStrategyDecision(
    NpcStrategyProfile Profile,
    double EffectiveFleeHpPercent,
    int EffectivePreferredRange)
{
    public NpcStrategyArchetype Archetype => Profile.Archetype;
}
