using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public enum NpcTacticalAction
{
    None = 0,
    BasicAttack = 1,
    Approach = 2,
    Flee = 3,
    CastSkill = 4,
    MaintainRange = 5,
    Retreat = 6
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
    int PreferredRange,
    int MaintainRangeScore = 0,
    int RetreatScore = 0)
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
        double targetDistance, double targetCollisionRadius,
        NpcStrategyCandidateEligibility? offensiveSkillSuppression) =>
        EvaluateCore(perception, profile, NpcTacticalPolicy.Baseline(profile), targetDistance,
            targetCollisionRadius, null, offensiveSkillSuppression);

    internal NpcTacticalScore Evaluate(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        NpcStrategyDecision strategy, double targetDistance, double targetCollisionRadius,
        NpcStrategyDiagnosticsCollector? diagnostics,
        NpcStrategyCandidateEligibility? offensiveSkillSuppression = null) =>
        EvaluateCore(perception, profile, NpcTacticalPolicy.FromStrategy(strategy), targetDistance,
            targetCollisionRadius, diagnostics, offensiveSkillSuppression);

    /// <summary>
    /// Test/diagnostic seam: evaluates with explicit MaintainRange and Retreat scores injected.
    /// Production callers use Baseline or FromStrategy (both default these scores to 0 = never selected).
    /// In 4B.5.9, NpcStrategyDirective will fill these values via NpcTacticalPolicy.
    /// </summary>
    internal NpcTacticalScore Evaluate(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        double targetDistance, double targetCollisionRadius,
        int suggestedRetreatRange, int maintainRangeScore, int retreatScore)
    {
        NpcTacticalPolicy policy = NpcTacticalPolicy.Baseline(profile) with
        {
            MaintainRangeScore = maintainRangeScore,
            RetreatScore = retreatScore
        };
        return EvaluateCore(perception, profile, policy, targetDistance, targetCollisionRadius,
            null, null, suggestedRetreatRange);
    }

    private static NpcTacticalScore EvaluateCore(NpcPerceptionSnapshot perception,
        NpcIntelligenceProfile profile, NpcTacticalPolicy policy, double targetDistance,
        double targetCollisionRadius, NpcStrategyDiagnosticsCollector? diagnostics,
        NpcStrategyCandidateEligibility? offensiveSkillSuppression = null,
        int? suggestedRetreatRange = null)
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

        int physicalAttackRange = Math.Max(1, perception.State.Combat.PhysicalAttackRange);
        double collisionPadding = perception.State.Physical.CollisionRadius + targetCollisionRadius;
        double physicalReach = physicalAttackRange + collisionPadding;
        NpcSkillObservation? offensive = SelectSkill(perception, NpcSkillCategory.Offensive, false);
        bool offensiveReady = offensive is { } offensiveSkill &&
            offensiveSkill.Flags.HasFlag(NpcSkillObservationFlags.Ready);
        bool offensiveInRange = false;
        if (offensiveReady)
        {
            // Offensive point-blank skills report cast range zero. They are usable only after reaching
            // physical contact; zero must never be interpreted as unlimited range.
            int skillRange = offensive!.Value.Range > 0
                ? offensive.Value.Range
                : physicalAttackRange;
            offensiveInRange = targetDistance <= skillRange + collisionPadding;
            diagnostics?.AddCandidate(new NpcStrategyCandidateScore(NpcStrategyAction.OffensiveSkill,
                NpcTacticalBaseline.OffensiveSkillScore, policy.OffensiveSkillScore,
                !offensiveInRange
                    ? NpcStrategyCandidateEligibility.OutOfRange
                    : offensiveSkillSuppression ?? NpcStrategyCandidateEligibility.Eligible,
                offensive.Value.SkillId, offensive.Value.Level));
            if (offensiveInRange)
            {
                if (offensiveSkillSuppression == null)
                {
                    selected = Prefer(selected, new NpcTacticalScore(NpcTacticalAction.CastSkill,
                        policy.OffensiveSkillScore, offensive));
                }
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
        selected = Prefer(selected, physical);

        // MaintainRange / Retreat — only eligible when a retreat range hint is provided (4B.5.9).
        // In Disabled / Static Strategy V1, suggestedRetreatRange is always null → scores = 0 →
        // these actions never compete, preserving the 4B.5.0 baseline exactly.
        if (suggestedRetreatRange is { } retreatRange && retreatRange > 0)
        {
            // Symmetric tolerance band: ±60 units around preferred range.
            // Without a band, "in-band" would be a single point and the NPC would oscillate.
            const int Tolerance = 60; // future: NPC_STRATEGY_RANGE_TOLERANCE configurable in 4B.5.8

            if (targetDistance < retreatRange - Tolerance && policy.MaintainRangeScore > 0)
            {
                // Target too close → retreat to preferred range.
                selected = Prefer(selected,
                    new NpcTacticalScore(NpcTacticalAction.MaintainRange, policy.MaintainRangeScore,
                        null, retreatRange));
            }
            else if (targetDistance > retreatRange + Tolerance && policy.MaintainRangeScore > 0)
            {
                // Target too far → approach with explicit range (score+1 makes priority explicit
                // over the generic physical Approach without a retreat hint).
                selected = Prefer(selected,
                    new NpcTacticalScore(NpcTacticalAction.Approach, policy.ApproachScore + 1,
                        null, retreatRange));
            }
            // else: in band [retreatRange-60, retreatRange+60] → no range movement emitted.

            if (policy.RetreatScore > 0)
            {
                // Pure tactical retreat — always move away to preferred range regardless of current distance.
                selected = Prefer(selected,
                    new NpcTacticalScore(NpcTacticalAction.Retreat, policy.RetreatScore,
                        null, retreatRange));
            }
        }

        return selected;
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
