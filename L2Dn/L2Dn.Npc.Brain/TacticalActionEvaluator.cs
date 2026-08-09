using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public enum NpcTacticalAction
{
    None = 0,
    BasicAttack = 1,
    Approach = 2,
    Flee = 3
}

public readonly record struct NpcTacticalScore(NpcTacticalAction Action, int Score);

public sealed class TacticalActionEvaluator
{
    public NpcTacticalScore Evaluate(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        double targetDistance)
    {
        if (profile.FleeAllowed && NpcPerceptionFacts.HpPercent(perception.State.Physical) <= profile.FleeHpPercent)
        {
            return new NpcTacticalScore(NpcTacticalAction.Flee, 100);
        }

        int preferredRange = profile.PreferredRange > 0
            ? profile.PreferredRange
            : Math.Max(1, perception.State.Combat.PhysicalAttackRange);
        return targetDistance > preferredRange
            ? new NpcTacticalScore(NpcTacticalAction.Approach, 80)
            : new NpcTacticalScore(NpcTacticalAction.BasicAttack, 60);
    }
}
