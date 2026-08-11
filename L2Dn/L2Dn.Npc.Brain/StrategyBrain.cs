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

        return new NpcStrategyDecision(strategy,
            Math.Clamp(fleeHpPercent, 0, 100), Math.Max(0, preferredRange));
    }
}
