using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

/// <summary>
/// Pure strategic policy layer. It converts a cached profile into bounded directives
/// consumed by Reflex/Tactical; it never creates or executes an intent itself.
/// </summary>
public sealed class StrategyBrain
{
    public NpcStrategyDecision Decide(NpcPerceptionSnapshot perception,
        NpcIntelligenceProfile intelligence, NpcStrategyProfile strategy)
    {
        ArgumentNullException.ThrowIfNull(perception);
        ArgumentNullException.ThrowIfNull(intelligence);
        ArgumentNullException.ThrowIfNull(strategy);

        double fleeHpPercent = strategy.FleeHpPercentOverride ?? intelligence.FleeHpPercent;
        int preferredRange = strategy.PreferredRangeOverride > 0
            ? strategy.PreferredRangeOverride
            : intelligence.PreferredRange;

        NpcStrategyModifierFlags modifiers = NpcStrategyModifierFlags.None;
        AddModifier(ref modifiers, NpcStrategyModifierFlags.BasicAttackScore,
            strategy.BasicAttackScore != NpcTacticalBaseline.BasicAttackScore);
        AddModifier(ref modifiers, NpcStrategyModifierFlags.ApproachScore,
            strategy.ApproachScore != NpcTacticalBaseline.ApproachScore);
        AddModifier(ref modifiers, NpcStrategyModifierFlags.OffensiveSkillScore,
            strategy.OffensiveSkillScore != NpcTacticalBaseline.OffensiveSkillScore);
        AddModifier(ref modifiers, NpcStrategyModifierFlags.HealScore,
            strategy.HealScore != NpcTacticalBaseline.HealScore);
        AddModifier(ref modifiers, NpcStrategyModifierFlags.FleeScore,
            strategy.FleeScore != NpcTacticalBaseline.FleeScore);
        AddModifier(ref modifiers, NpcStrategyModifierFlags.HealHpPercent,
            strategy.HealHpPercent != NpcTacticalBaseline.HealHpPercent);
        AddModifier(ref modifiers, NpcStrategyModifierFlags.FleeHpPercent,
            fleeHpPercent != intelligence.FleeHpPercent);
        AddModifier(ref modifiers, NpcStrategyModifierFlags.PreferredRange,
            preferredRange != intelligence.PreferredRange);

        return new NpcStrategyDecision(strategy,
            Math.Clamp(fleeHpPercent, 0, 100), Math.Max(0, preferredRange), modifiers);
    }

    private static void AddModifier(ref NpcStrategyModifierFlags modifiers,
        NpcStrategyModifierFlags modifier, bool applied)
    {
        if (applied)
        {
            modifiers |= modifier;
        }
    }
}
