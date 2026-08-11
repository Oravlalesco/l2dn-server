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

public sealed class TacticalActionEvaluator
{
    public NpcTacticalScore Evaluate(NpcPerceptionSnapshot perception, NpcIntelligenceProfile profile,
        double targetDistance, double targetCollisionRadius = 0)
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
                    NpcSkillCategory.Control) && skill.Flags.HasFlag(NpcSkillObservationFlags.Ready))
            .OrderByDescending(static skill => skill.Level)
            .ThenBy(static skill => skill.SkillId)
            .Cast<NpcSkillObservation?>()
            .FirstOrDefault();
        if (offensive.HasValue)
        {
            int skillRange = Math.Max(0, offensive.Value.Range);
            double collisionPadding = perception.State.Physical.CollisionRadius + targetCollisionRadius;
            if (skillRange <= 0 || targetDistance <= skillRange + collisionPadding)
            {
                return new NpcTacticalScore(NpcTacticalAction.CastSkill, 90, offensive);
            }

            int desiredRange = profile.PreferredRange > 0
                ? Math.Min(profile.PreferredRange, skillRange)
                : skillRange;
            return new NpcTacticalScore(NpcTacticalAction.Approach, 80, offensive,
                Math.Max(1, desiredRange));
        }

        int physicalAttackRange = Math.Max(1, perception.State.Combat.PhysicalAttackRange);
        double physicalReach = physicalAttackRange + perception.State.Physical.CollisionRadius +
            targetCollisionRadius;
        return targetDistance > physicalReach
            ? new NpcTacticalScore(NpcTacticalAction.Approach, 80, null, physicalAttackRange)
            : new NpcTacticalScore(NpcTacticalAction.BasicAttack, 60);
    }
}
