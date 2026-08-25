using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

/// <summary>
/// Derived facts consumed by StrategicUtilityEvaluator (4B.5.6).
/// All fields are computed from NpcPerceptionSnapshot + NpcIntelligenceProfile;
/// no I/O, no heap allocation, fully deterministic.
///
/// Fields that require NpcStrategyState (CombatDurationTicks, CurrentPosture,
/// TimeInPostureTicks) default to Neutral/0 until 4B.5.8 wires them in.
/// </summary>
internal readonly record struct NpcStrategyEvaluationContext(
    // Self (0-1000 fixed-point ratios; not 0-100)
    int SelfHpRatio,
    int SelfMpRatio,
    bool InCombat,
    int CombatDurationTicks,
    // Target
    bool HasTarget,
    int TargetDistance,
    int PreferredRange,
    bool TargetWithinRange,
    bool TargetClose,
    bool TargetFar,
    // Capabilities
    bool OffensiveSkillReady,
    bool HealReady,
    bool CanFlee,
    // Environment
    int VisibleHostileCount,
    int NearbyAllyCount,
    // Posture state (populated from NpcStrategyState in 4B.5.8)
    NpcStrategicPosture CurrentPosture,
    int TimeInPostureTicks);

/// <summary>
/// Builds an NpcStrategyEvaluationContext from a perception snapshot.
/// Stateful parameters (currentPosture, timeInPostureTicks, combatDurationTicks)
/// are passed as arguments so the builder remains pure and testable without NpcBrainState.
/// </summary>
internal static class StrategyContextBuilder
{
    /// <summary>Tolerance band half-width for TargetWithinRange (to be promoted to config in 4B.5.8).</summary>
    private const int RangeTolerance = 60;

    public static NpcStrategyEvaluationContext Build(
        NpcPerceptionSnapshot perception,
        NpcIntelligenceProfile profile,
        NpcStrategicPosture currentPosture = NpcStrategicPosture.Neutral,
        int timeInPostureTicks = 0,
        int combatDurationTicks = 0)
    {
        NpcPhysicalState physical = perception.State.Physical;
        NpcCombatFacts combat = perception.State.Combat;

        // Ratios 0-1000 (HpPercent returns 0-100, so multiply by 10)
        int hpRatio = (int)(NpcPerceptionFacts.HpPercent(physical) * 10);
        int mpRatio = physical.MaximumMp <= 0 ? 0
            : (int)(physical.CurrentMp / physical.MaximumMp * 1000);

        NpcCombatFlags combatFlags = combat.Flags;
        bool inCombat = (combatFlags & NpcCombatFlags.InCombat) != 0;
        bool hasTarget = combat.CurrentTarget is { };

        // Target distance and proximity bands
        int targetDistance = 0;
        bool targetWithinRange = false;
        bool targetClose = false;
        bool targetFar = false;
        int preferredRange = profile.PreferredRange;

        if (hasTarget && combat.CurrentTarget is { } targetKey &&
            NpcPerceptionFacts.TryGetVisibleEntity(perception, targetKey, out VisibleEntity visible))
        {
            targetDistance = (int)visible.Distance2D;

            if (preferredRange > 0)
            {
                targetWithinRange = targetDistance >= preferredRange - RangeTolerance &&
                    targetDistance <= preferredRange + RangeTolerance;
                targetClose = targetDistance < preferredRange / 2;
                targetFar = targetDistance > preferredRange * 3 / 2;
            }
        }

        // Skill readiness — bitwise flags to avoid Enum.HasFlag boxing
        bool offensiveSkillReady = false;
        bool healReady = false;
        foreach (NpcSkillObservation skill in perception.State.Skills)
        {
            if ((skill.Flags & NpcSkillObservationFlags.Ready) == 0)
            {
                continue;
            }

            switch (skill.Category)
            {
                case NpcSkillCategory.Offensive:
                case NpcSkillCategory.Debuff:
                case NpcSkillCategory.Control:
                    offensiveSkillReady = true;
                    break;
                case NpcSkillCategory.Heal:
                    healReady = true;
                    break;
            }
        }

        // Visible hostiles and allies — bitwise flags to avoid boxing
        int hostileCount = 0;
        int allyCount = 0;
        foreach (VisibleEntity entity in perception.State.VisibleEntities)
        {
            EntityRelationFlags relations = entity.Relations;
            if ((relations & EntityRelationFlags.SameClan) != 0 &&
                (relations & EntityRelationFlags.Npc) != 0)
            {
                allyCount++;
            }
            else if ((relations & EntityRelationFlags.Playable) != 0 &&
                (relations & EntityRelationFlags.AutoAttackable) != 0)
            {
                hostileCount++;
            }
        }

        return new NpcStrategyEvaluationContext(
            SelfHpRatio: hpRatio,
            SelfMpRatio: mpRatio,
            InCombat: inCombat,
            CombatDurationTicks: combatDurationTicks,
            HasTarget: hasTarget,
            TargetDistance: targetDistance,
            PreferredRange: preferredRange,
            TargetWithinRange: targetWithinRange,
            TargetClose: targetClose,
            TargetFar: targetFar,
            OffensiveSkillReady: offensiveSkillReady,
            HealReady: healReady,
            CanFlee: profile.FleeAllowed,
            VisibleHostileCount: hostileCount,
            NearbyAllyCount: allyCount,
            CurrentPosture: currentPosture,
            TimeInPostureTicks: timeInPostureTicks);
    }
}
