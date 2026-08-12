using System.Collections.Immutable;
using L2Dn.NpcContracts;

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
    int EffectivePreferredRange,
    NpcStrategyModifierFlags AppliedModifiers)
{
    public NpcStrategyArchetype Archetype => Profile.Archetype;
}

public enum NpcStrategyAction
{
    None = 0,
    BasicAttack = 1,
    Approach = 2,
    OffensiveSkill = 3,
    Heal = 4,
    Flee = 5
}

public enum NpcStrategyDecisionReason
{
    None = 0,
    ReflexAuthority = 1,
    HighestEligibleScore = 2,
    ActorUnavailable = 3,
    TargetUnavailable = 4,
    NoEligibleAction = 5
}

public readonly record struct NpcStrategyDecisionSummary(
    NpcStrategyArchetype Profile,
    NpcStrategyAction SelectedAction,
    NpcStrategyDecisionReason Reason,
    NpcStrategyModifierFlags AppliedModifiers);

[Flags]
public enum NpcStrategyModifierFlags
{
    None = 0,
    BasicAttackScore = 1 << 0,
    ApproachScore = 1 << 1,
    OffensiveSkillScore = 1 << 2,
    HealScore = 1 << 3,
    FleeScore = 1 << 4,
    HealHpPercent = 1 << 5,
    FleeHpPercent = 1 << 6,
    PreferredRange = 1 << 7
}

public enum NpcStrategyCandidateEligibility
{
    Eligible = 0,
    HpThresholdNotMet = 1,
    SkillUnavailable = 2,
    OutOfRange = 3,
    ActorUnavailable = 4,
    TargetUnavailable = 5
}

public readonly record struct NpcStrategyCandidateScore(
    NpcStrategyAction Action,
    int BaselineScore,
    int EffectiveScore,
    NpcStrategyCandidateEligibility Eligibility,
    int? SkillId = null,
    int? SkillLevel = null);

public readonly record struct NpcStrategyAppliedModifier(
    NpcStrategyModifierFlags Modifier,
    double BaselineValue,
    double EffectiveValue);

public sealed record NpcStrategyDecisionDiagnostics(
    ImmutableArray<NpcStrategyCandidateScore> CandidateScores,
    ImmutableArray<NpcStrategyAppliedModifier> AppliedModifiers)
{
    public static NpcStrategyDecisionDiagnostics Empty { get; } = new([], []);
}

public sealed record StrategyReplayResult(
    long SnapshotRevision,
    NpcStrategyArchetype Profile,
    NpcStrategyAction SelectedAction,
    NpcIntent? SelectedIntent,
    ImmutableArray<NpcStrategyCandidateScore> CandidateScores,
    ImmutableArray<NpcStrategyAppliedModifier> AppliedModifiers,
    NpcStrategyDecisionReason DecisionReason,
    TimeSpan DecisionDuration);

public sealed record NpcStrategyShadowEvaluation(
    NpcBrainDecision BaselineDecision,
    NpcBrainDecision? StrategyDecision,
    bool StrategyFailed,
    TimeSpan StrategyDecisionDuration);

internal static class NpcTacticalBaseline
{
    public const int BasicAttackScore = 60;
    public const int ApproachScore = 80;
    public const int OffensiveSkillScore = 90;
    public const int HealScore = 100;
    public const int FleeScore = 100;
    public const double HealHpPercent = 35;
}

internal sealed class NpcStrategyDiagnosticsCollector
{
    private readonly List<NpcStrategyCandidateScore> _candidateScores = [];

    public void AddCandidate(NpcStrategyCandidateScore candidate) => _candidateScores.Add(candidate);

    public NpcStrategyDecisionDiagnostics Build(NpcIntelligenceProfile intelligence,
        NpcStrategyDecision strategy)
    {
        ImmutableArray<NpcStrategyAppliedModifier>.Builder modifiers =
            ImmutableArray.CreateBuilder<NpcStrategyAppliedModifier>();
        AddModifier(modifiers, NpcStrategyModifierFlags.BasicAttackScore,
            NpcTacticalBaseline.BasicAttackScore, strategy.Profile.BasicAttackScore);
        AddModifier(modifiers, NpcStrategyModifierFlags.ApproachScore,
            NpcTacticalBaseline.ApproachScore, strategy.Profile.ApproachScore);
        AddModifier(modifiers, NpcStrategyModifierFlags.OffensiveSkillScore,
            NpcTacticalBaseline.OffensiveSkillScore, strategy.Profile.OffensiveSkillScore);
        AddModifier(modifiers, NpcStrategyModifierFlags.HealScore,
            NpcTacticalBaseline.HealScore, strategy.Profile.HealScore);
        AddModifier(modifiers, NpcStrategyModifierFlags.FleeScore,
            NpcTacticalBaseline.FleeScore, strategy.Profile.FleeScore);
        AddModifier(modifiers, NpcStrategyModifierFlags.HealHpPercent,
            NpcTacticalBaseline.HealHpPercent, strategy.Profile.HealHpPercent);
        AddModifier(modifiers, NpcStrategyModifierFlags.FleeHpPercent,
            intelligence.FleeHpPercent, strategy.EffectiveFleeHpPercent);
        AddModifier(modifiers, NpcStrategyModifierFlags.PreferredRange,
            intelligence.PreferredRange, strategy.EffectivePreferredRange);
        return new NpcStrategyDecisionDiagnostics(_candidateScores.ToImmutableArray(), modifiers.ToImmutable());
    }

    private static void AddModifier(ImmutableArray<NpcStrategyAppliedModifier>.Builder modifiers,
        NpcStrategyModifierFlags modifier, double baseline, double effective)
    {
        if (baseline != effective)
        {
            modifiers.Add(new NpcStrategyAppliedModifier(modifier, baseline, effective));
        }
    }
}
