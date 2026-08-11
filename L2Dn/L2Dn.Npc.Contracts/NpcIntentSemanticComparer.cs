namespace L2Dn.NpcContracts;

/// <summary>
/// Compares requested behavior while deliberately ignoring publication revision and decision sequence metadata.
/// </summary>
public sealed class NpcIntentSemanticComparer: IEqualityComparer<NpcIntent>
{
    public static NpcIntentSemanticComparer Instance { get; } = new();

    private NpcIntentSemanticComparer()
    {
    }

    public bool Equals(NpcIntent? x, NpcIntent? y)
    {
        if (ReferenceEquals(x, y))
        {
            return true;
        }

        if (x is null || y is null || x.Envelope.Actor != y.Envelope.Actor ||
            x.Envelope.IntentType != y.Envelope.IntentType)
        {
            return false;
        }

        return (x, y) switch
        {
            (AcquireTargetIntent left, AcquireTargetIntent right) => left.Target == right.Target &&
                left.Mode == right.Mode,
            (ClearTargetIntent left, ClearTargetIntent right) => left.ExpectedTarget == right.ExpectedTarget,
            (BasicAttackIntent left, BasicAttackIntent right) => left.Target == right.Target,
            (ApproachTargetIntent left, ApproachTargetIntent right) => left.Target == right.Target &&
                left.Constraint == right.Constraint,
            (ReturnHomeIntent left, ReturnHomeIntent right) => left.Mode == right.Mode,
            (FleeIntent left, FleeIntent right) => left.Threat == right.Threat,
            (CastSkillIntent left, CastSkillIntent right) => left.SkillId == right.SkillId &&
                left.SkillLevel == right.SkillLevel && left.Target == right.Target,
            (StopCombatIntent, StopCombatIntent) => true,
            _ => false
        };
    }

    public int GetHashCode(NpcIntent obj)
    {
        HashCode hash = new();
        hash.Add(obj.Envelope.Actor);
        hash.Add(obj.Envelope.IntentType);
        switch (obj)
        {
            case AcquireTargetIntent acquire:
                hash.Add(acquire.Target);
                hash.Add(acquire.Mode);
                break;
            case ClearTargetIntent clear:
                hash.Add(clear.ExpectedTarget);
                break;
            case BasicAttackIntent attack:
                hash.Add(attack.Target);
                break;
            case ApproachTargetIntent approach:
                hash.Add(approach.Target);
                hash.Add(approach.Constraint);
                break;
            case ReturnHomeIntent returnHome:
                hash.Add(returnHome.Mode);
                break;
            case FleeIntent flee:
                hash.Add(flee.Threat);
                break;
            case CastSkillIntent cast:
                hash.Add(cast.SkillId);
                hash.Add(cast.SkillLevel);
                hash.Add(cast.Target);
                break;
        }
        return hash.ToHashCode();
    }
}
