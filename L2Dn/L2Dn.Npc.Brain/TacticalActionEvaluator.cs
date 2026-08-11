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
        NpcStrategyDecision strategy, double targetDistance, double targetCollisionRadius = 0)
    {
        NpcTacticalScore selected = default;
        double hpPercent = NpcPerceptionFacts.HpPercent(perception.State.Physical);
        if (profile.FleeAllowed && hpPercent <= strategy.EffectiveFleeHpPercent)
        {
            selected = Prefer(selected,
                new NpcTacticalScore(NpcTacticalAction.Flee, strategy.Profile.FleeScore));
        }

        NpcSkillObservation? heal = perception.State.Skills
            .Where(static skill => skill.Category == NpcSkillCategory.Heal &&
                skill.Flags.HasFlag(NpcSkillObservationFlags.Ready))
            .OrderByDescending(static skill => skill.Level)
            .ThenBy(static skill => skill.SkillId)
            .Cast<NpcSkillObservation?>()
            .FirstOrDefault();
        if (heal.HasValue && hpPercent <= Math.Clamp(strategy.Profile.HealHpPercent, 0, 100))
        {
            selected = Prefer(selected, new NpcTacticalScore(NpcTacticalAction.CastSkill,
                strategy.Profile.HealScore, heal));
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
                selected = Prefer(selected, new NpcTacticalScore(NpcTacticalAction.CastSkill,
                    strategy.Profile.OffensiveSkillScore, offensive));
            }
            else
            {
                int desiredRange = strategy.EffectivePreferredRange > 0
                    ? Math.Min(strategy.EffectivePreferredRange, skillRange)
                    : skillRange;
                selected = Prefer(selected, new NpcTacticalScore(NpcTacticalAction.Approach,
                    strategy.Profile.ApproachScore, offensive, Math.Max(1, desiredRange)));
            }
        }

        int physicalAttackRange = Math.Max(1, perception.State.Combat.PhysicalAttackRange);
        double physicalReach = physicalAttackRange + perception.State.Physical.CollisionRadius +
            targetCollisionRadius;
        NpcTacticalScore physical = targetDistance > physicalReach
            ? new NpcTacticalScore(NpcTacticalAction.Approach, strategy.Profile.ApproachScore,
                null, physicalAttackRange)
            : new NpcTacticalScore(NpcTacticalAction.BasicAttack, strategy.Profile.BasicAttackScore);
        return Prefer(selected, physical);
    }

    private static NpcTacticalScore Prefer(NpcTacticalScore current, NpcTacticalScore candidate) =>
        candidate.Score > current.Score ? candidate : current;
}
