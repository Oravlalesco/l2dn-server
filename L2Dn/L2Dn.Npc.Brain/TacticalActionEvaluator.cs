using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public enum NpcTacticalAction
{
    None = 0,
    BasicAttack = 1,
    Approach = 2,
    Flee = 3,
    CastSkill = 4
}

public readonly record struct NpcTacticalScore(
    NpcTacticalAction Action,
    int Score,
    NpcSkillObservation? Skill = null,
    int DesiredRange = 0);

internal readonly record struct NpcTacticalPolicy(
    int BasicAttackScore,
    int ApproachScore,
    int OffensiveSkillScore,
    int HealScore,
    int FleeScore,
    double HealHpPercent,
    double FleeHpPercent,
    int PreferredRange)
{
    public static NpcTacticalPolicy Baseline(NpcIntelligenceProfile profile) => new(
        NpcTacticalBaseline.BasicAttackScore,
        NpcTacticalBaseline.ApproachScore,
        NpcTacticalBaseline.OffensiveSkillScore,
        NpcTacticalBaseline.HealScore,
        NpcTacticalBaseline.FleeScore,
        NpcTacticalBaseline.HealHpPercent,
        profile.FleeHpPercent,
        profile.PreferredRange);

    public static NpcTacticalPolicy FromStrategy(NpcStrategyDecision strategy) => new(
        strategy.Profile.BasicAttackScore,
        strategy.Profile.ApproachScore,
        strategy.Profile.OffensiveSkillScore,
        strategy.Profile.HealScore,
        strategy.Profile.FleeScore,
        strategy.Profile.HealHpPercent,
        strategy.EffectiveFleeHpPercent,
        strategy.EffectivePreferredRange);
}

public sealed class TacticalActionEvaluator
{
    public NpcTacticalScore Evaluate(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        double targetDistance, double targetCollisionRadius = 0) =>
        EvaluateCore(perception, profile, NpcTacticalPolicy.Baseline(profile), targetDistance,
            targetCollisionRadius, null);

    public NpcTacticalScore Evaluate(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        NpcStrategyDecision strategy, double targetDistance, double targetCollisionRadius = 0) =>
        EvaluateCore(perception, profile, NpcTacticalPolicy.FromStrategy(strategy), targetDistance,
            targetCollisionRadius, null);

    internal NpcTacticalScore Evaluate(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        NpcStrategyDecision strategy, double targetDistance, double targetCollisionRadius,
        NpcStrategyDiagnosticsCollector? diagnostics) =>
        EvaluateCore(perception, profile, NpcTacticalPolicy.FromStrategy(strategy), targetDistance,
            targetCollisionRadius, diagnostics);

    private static NpcTacticalScore EvaluateCore(NpcPerceptionSnapshot perception,
        NpcIntelligenceProfile profile, NpcTacticalPolicy policy, double targetDistance,
        double targetCollisionRadius, NpcStrategyDiagnosticsCollector? diagnostics)
    {
        NpcTacticalScore selected = default;
        double hpPercent = NpcPerceptionFacts.HpPercent(perception.State.Physical);
        bool fleeEligible = profile.FleeAllowed && hpPercent <= policy.FleeHpPercent;
        diagnostics?.AddCandidate(new NpcStrategyCandidateScore(NpcStrategyAction.Flee,
            NpcTacticalBaseline.FleeScore, policy.FleeScore,
            fleeEligible ? NpcStrategyCandidateEligibility.Eligible :
                NpcStrategyCandidateEligibility.HpThresholdNotMet));
        if (fleeEligible)
        {
            selected = Prefer(selected, new NpcTacticalScore(NpcTacticalAction.Flee, policy.FleeScore));
        }

        NpcSkillObservation? heal = SelectSkill(perception, NpcSkillCategory.Heal, true);
        bool healReady = heal is { } healSkill && healSkill.Flags.HasFlag(NpcSkillObservationFlags.Ready);
        bool healHpEligible = hpPercent <= Math.Clamp(policy.HealHpPercent, 0, 100);
        diagnostics?.AddCandidate(new NpcStrategyCandidateScore(NpcStrategyAction.Heal,
            NpcTacticalBaseline.HealScore, policy.HealScore,
            !healReady ? NpcStrategyCandidateEligibility.SkillUnavailable :
            healHpEligible ? NpcStrategyCandidateEligibility.Eligible :
                NpcStrategyCandidateEligibility.HpThresholdNotMet,
            heal?.SkillId, heal?.Level));
        if (healReady && healHpEligible)
        {
            selected = Prefer(selected,
                new NpcTacticalScore(NpcTacticalAction.CastSkill, policy.HealScore, heal));
        }

        NpcSkillObservation? offensive = SelectSkill(perception, NpcSkillCategory.Offensive, false);
        bool offensiveReady = offensive is { } offensiveSkill &&
            offensiveSkill.Flags.HasFlag(NpcSkillObservationFlags.Ready);
        bool offensiveInRange = false;
        if (offensiveReady)
        {
            int skillRange = Math.Max(0, offensive!.Value.Range);
            double collisionPadding = perception.State.Physical.CollisionRadius + targetCollisionRadius;
            offensiveInRange = skillRange <= 0 || targetDistance <= skillRange + collisionPadding;
            diagnostics?.AddCandidate(new NpcStrategyCandidateScore(NpcStrategyAction.OffensiveSkill,
                NpcTacticalBaseline.OffensiveSkillScore, policy.OffensiveSkillScore,
                offensiveInRange ? NpcStrategyCandidateEligibility.Eligible :
                    NpcStrategyCandidateEligibility.OutOfRange,
                offensive.Value.SkillId, offensive.Value.Level));
            if (offensiveInRange)
            {
                selected = Prefer(selected, new NpcTacticalScore(NpcTacticalAction.CastSkill,
                    policy.OffensiveSkillScore, offensive));
            }
            else
            {
                int desiredRange = policy.PreferredRange > 0
                    ? Math.Min(policy.PreferredRange, skillRange)
                    : skillRange;
                diagnostics?.AddCandidate(new NpcStrategyCandidateScore(NpcStrategyAction.Approach,
                    NpcTacticalBaseline.ApproachScore, policy.ApproachScore,
                    NpcStrategyCandidateEligibility.Eligible, offensive.Value.SkillId,
                    offensive.Value.Level));
                selected = Prefer(selected, new NpcTacticalScore(NpcTacticalAction.Approach,
                    policy.ApproachScore, offensive, Math.Max(1, desiredRange)));
            }
        }
        else
        {
            diagnostics?.AddCandidate(new NpcStrategyCandidateScore(NpcStrategyAction.OffensiveSkill,
                NpcTacticalBaseline.OffensiveSkillScore, policy.OffensiveSkillScore,
                NpcStrategyCandidateEligibility.SkillUnavailable,
                offensive?.SkillId, offensive?.Level));
        }

        int physicalAttackRange = Math.Max(1, perception.State.Combat.PhysicalAttackRange);
        double physicalReach = physicalAttackRange + perception.State.Physical.CollisionRadius +
            targetCollisionRadius;
        bool needsApproach = targetDistance > physicalReach;
        NpcStrategyAction physicalAction = needsApproach
            ? NpcStrategyAction.Approach
            : NpcStrategyAction.BasicAttack;
        int physicalBaselineScore = needsApproach
            ? NpcTacticalBaseline.ApproachScore
            : NpcTacticalBaseline.BasicAttackScore;
        int physicalEffectiveScore = needsApproach ? policy.ApproachScore : policy.BasicAttackScore;
        diagnostics?.AddCandidate(new NpcStrategyCandidateScore(physicalAction,
            physicalBaselineScore, physicalEffectiveScore, NpcStrategyCandidateEligibility.Eligible));
        NpcTacticalScore physical = needsApproach
            ? new NpcTacticalScore(NpcTacticalAction.Approach, policy.ApproachScore,
                null, physicalAttackRange)
            : new NpcTacticalScore(NpcTacticalAction.BasicAttack, policy.BasicAttackScore);
        return Prefer(selected, physical);
    }

    private static NpcSkillObservation? SelectSkill(NpcPerceptionSnapshot perception,
        NpcSkillCategory category, bool exactCategory) => perception.State.Skills
        .Where(skill => exactCategory
            ? skill.Category == category
            : skill.Category is NpcSkillCategory.Offensive or NpcSkillCategory.Debuff or
                NpcSkillCategory.Control)
        .OrderByDescending(static skill => skill.Flags.HasFlag(NpcSkillObservationFlags.Ready))
        .ThenByDescending(static skill => skill.Level)
        .ThenBy(static skill => skill.SkillId)
        .Cast<NpcSkillObservation?>()
        .FirstOrDefault();

    private static NpcTacticalScore Prefer(NpcTacticalScore current, NpcTacticalScore candidate) =>
        candidate.Score > current.Score ? candidate : current;
}
