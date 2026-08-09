using L2Dn.NpcContracts;

namespace L2Dn.NpcBrain;

public sealed class NpcIntelligenceProfileResolver
{
    private static readonly NpcIntelligenceProfile Passive = new(
        NpcIntelligenceArchetype.BasicPassiveMob, true, true, false, false, 0, 0, 200);

    private static readonly NpcIntelligenceProfile Aggressive = new(
        NpcIntelligenceArchetype.BasicAggressiveMob, true, true, true, false, 0, 0, 200);

    private static readonly NpcIntelligenceProfile Caster = new(
        NpcIntelligenceArchetype.BasicCasterMob, true, true, true, false, 0, 400, 200);

    private static readonly NpcIntelligenceProfile Melee = new(
        NpcIntelligenceArchetype.BasicMeleeMob, true, true, true, false, 0, 0, 200);

    public static NpcIntelligenceProfileResolver Instance { get; } = new();

    private NpcIntelligenceProfileResolver()
    {
    }

    public NpcIntelligenceProfile Resolve(NpcIdentity identity)
    {
        bool aggressive = identity.Capabilities.HasFlag(NpcCapabilities.Aggressive) ||
            identity.Capabilities.HasFlag(NpcCapabilities.Guard);
        if (!aggressive)
        {
            return Passive;
        }

        if (identity.LegacyAiType is LegacyNpcAiType.Mage or LegacyNpcAiType.Healer)
        {
            return Caster;
        }

        if (identity.LegacyAiType == LegacyNpcAiType.Fighter)
        {
            return Melee;
        }

        return Aggressive;
    }
}
