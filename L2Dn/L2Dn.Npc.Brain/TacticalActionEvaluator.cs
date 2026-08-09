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
    NpcSkillObservation? Skill = null);

public sealed class TacticalActionEvaluator
{
    public NpcTacticalScore Evaluate(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        double targetDistance)
    {
        if (profile.FleeAllowed && NpcPerceptionFacts.HpPercent(perception.State.Physical) <= profile.FleeHpPercent)
        {
            return new NpcTacticalScore(NpcTacticalAction.Flee, 100);
        }

        NpcSkillObservation? heal = perception.State.Skills
            .Where(static skill => skill.Category == NpcSkillCategory.Heal &&
                skill.Flags.HasFlag(NpcSkillObservationFlags.Ready))
            .OrderByDescending(static skill => skill.Level)
            .ThenBy(static skill => skill.SkillId)
            .Cast<NpcSkillObservation?>()
            .FirstOrDefault();
        if (heal.HasValue && NpcPerceptionFacts.HpPercent(perception.State.Physical) <= 35)
        {
            return new NpcTacticalScore(NpcTacticalAction.CastSkill, 100, heal);
        }

        NpcSkillObservation? offensive = perception.State.Skills
            .Where(skill => (skill.Category is NpcSkillCategory.Offensive or NpcSkillCategory.Debuff or
                    NpcSkillCategory.Control) && skill.Flags.HasFlag(NpcSkillObservationFlags.Ready) &&
                (skill.Range <= 0 || targetDistance <= skill.Range + perception.State.Physical.CollisionRadius))
            .OrderByDescending(static skill => skill.Level)
            .ThenBy(static skill => skill.SkillId)
            .Cast<NpcSkillObservation?>()
            .FirstOrDefault();
        if (offensive.HasValue)
        {
            return new NpcTacticalScore(NpcTacticalAction.CastSkill, 90, offensive);
        }

        int preferredRange = profile.PreferredRange > 0
            ? profile.PreferredRange
            : Math.Max(1, perception.State.Combat.PhysicalAttackRange);
        return targetDistance > preferredRange
            ? new NpcTacticalScore(NpcTacticalAction.Approach, 80)
            : new NpcTacticalScore(NpcTacticalAction.BasicAttack, 60);
    }
}
