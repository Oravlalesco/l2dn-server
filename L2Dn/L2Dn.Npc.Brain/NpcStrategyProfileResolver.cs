using System.Collections.Immutable;
using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class NpcStrategyProfileResolver
{
    public static NpcStrategyProfile Balanced { get; } = new(
        "balanced-v1", NpcStrategyArchetype.Balanced, 60, 80, 90, 100, 100, 35);

    public static NpcStrategyProfile AggressivePressure { get; } = new(
        "aggressive-pressure-v1", NpcStrategyArchetype.AggressivePressure,
        75, 90, 105, 85, 70, 25, 5);

    public static NpcStrategyProfile RangedControl { get; } = new(
        "ranged-control-v1", NpcStrategyArchetype.RangedControl,
        45, 95, 110, 100, 100, 35, null, 600);

    public static NpcStrategyProfile Survival { get; } = new(
        "survival-v1", NpcStrategyArchetype.Survival,
        45, 75, 80, 120, 115, 50, 30);

    private readonly ImmutableDictionary<int, NpcStrategyProfile> _templateOverrides;

    public static NpcStrategyProfileResolver Instance { get; } = new();

    public NpcStrategyProfileResolver(IReadOnlyDictionary<int, NpcStrategyProfile>? templateOverrides = null)
    {
        _templateOverrides = templateOverrides == null
            ? ImmutableDictionary<int, NpcStrategyProfile>.Empty
            : templateOverrides.ToImmutableDictionary();
    }

    public NpcStrategyProfile Resolve(NpcIdentity identity, NpcIntelligenceProfile intelligence)
    {
        if (_templateOverrides.TryGetValue(identity.TemplateId, out NpcStrategyProfile? profile))
        {
            return profile;
        }

        if (identity.LegacyAiType == LegacyNpcAiType.Healer)
        {
            return Survival;
        }

        return intelligence.Archetype switch
        {
            NpcIntelligenceArchetype.BasicCasterMob => RangedControl,
            NpcIntelligenceArchetype.BasicMeleeMob => AggressivePressure,
            _ => Balanced
        };
    }
}
