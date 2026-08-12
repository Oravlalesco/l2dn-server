using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public enum NpcStrategyComparisonKind
{
    ExactMatch = 1,
    SemanticMatch = 2,
    DifferentAction = 3,
    DifferentTarget = 4,
    DifferentSkill = 5,
    DifferentMovement = 6,
    NotComparable = 7
}

public readonly record struct StrategyComparisonResult(NpcStrategyComparisonKind Kind)
{
    public bool ChangedDecision => Kind is NpcStrategyComparisonKind.DifferentAction or
        NpcStrategyComparisonKind.DifferentTarget or NpcStrategyComparisonKind.DifferentSkill or
        NpcStrategyComparisonKind.DifferentMovement;
}

public static class NpcStrategyDecisionComparer
{
    public static StrategyComparisonResult Compare(NpcBrainDecision baseline, NpcBrainDecision strategy)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(strategy);
        if (baseline.Actor != strategy.Actor || baseline.Intents.Length > 1 || strategy.Intents.Length > 1)
        {
            return Result(NpcStrategyComparisonKind.NotComparable);
        }

        if (baseline.Intents.IsEmpty && strategy.Intents.IsEmpty)
        {
            return Result(NpcStrategyComparisonKind.ExactMatch);
        }
        if (baseline.Intents.IsEmpty || strategy.Intents.IsEmpty)
        {
            return Result(NpcStrategyComparisonKind.DifferentAction);
        }

        NpcIntent left = baseline.Intents[0];
        NpcIntent right = strategy.Intents[0];
        if (left.Envelope.IntentType != right.Envelope.IntentType)
        {
            return Result(NpcStrategyComparisonKind.DifferentAction);
        }
        if (GetTarget(left) != GetTarget(right))
        {
            return Result(NpcStrategyComparisonKind.DifferentTarget);
        }
        if (left is CastSkillIntent leftSkill && right is CastSkillIntent rightSkill &&
            (leftSkill.SkillId != rightSkill.SkillId || leftSkill.SkillLevel != rightSkill.SkillLevel))
        {
            return Result(NpcStrategyComparisonKind.DifferentSkill);
        }
        if (!HasSameMovementSemantics(left, right))
        {
            return Result(NpcStrategyComparisonKind.DifferentMovement);
        }
        if (Equals(left, right))
        {
            return Result(NpcStrategyComparisonKind.ExactMatch);
        }
        return NpcIntentSemanticComparer.Instance.Equals(left, right)
            ? Result(NpcStrategyComparisonKind.SemanticMatch)
            : Result(NpcStrategyComparisonKind.NotComparable);
    }

    private static EntityKey? GetTarget(NpcIntent intent) => intent switch
    {
        AcquireTargetIntent acquire => acquire.Target,
        ClearTargetIntent clear => clear.ExpectedTarget,
        BasicAttackIntent attack => attack.Target,
        ApproachTargetIntent approach => approach.Target,
        FleeIntent flee => flee.Threat,
        CastSkillIntent cast => cast.Target,
        _ => null
    };

    private static bool HasSameMovementSemantics(NpcIntent left, NpcIntent right) => (left, right) switch
    {
        (AcquireTargetIntent x, AcquireTargetIntent y) => x.Mode == y.Mode,
        (ApproachTargetIntent x, ApproachTargetIntent y) =>
            x.PreferredRange == y.PreferredRange && x.Constraint == y.Constraint,
        (ReturnHomeIntent x, ReturnHomeIntent y) => x.Mode == y.Mode,
        _ => true
    };

    private static StrategyComparisonResult Result(NpcStrategyComparisonKind kind) => new(kind);
}
