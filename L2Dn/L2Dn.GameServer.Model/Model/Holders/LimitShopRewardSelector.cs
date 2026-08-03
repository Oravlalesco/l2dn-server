namespace L2Dn.GameServer.Model.Holders;

public readonly record struct LimitShopRewardOutcome(
    int Index,
    int ItemId,
    long Count,
    int Enchant,
    bool Announce);

public static class LimitShopRewardSelector
{
    public static LimitShopRewardOutcome? Select(LimitShopProductHolder product, double roll)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (roll < 0 || roll >= 100)
            throw new ArgumentOutOfRangeException(nameof(roll), roll, "Roll must be in the [0, 100) range.");

        LimitShopRewardOutcome[] outcomes = GetOutcomes(product);
        if (outcomes.Length == 1)
            return roll < product.getChance() ? outcomes[0] : null;

        float[] chances =
        [
            product.getChance(),
            product.getChance2(),
            product.getChance3(),
            product.getChance4(),
        ];

        double cumulativeChance = 0;
        for (int index = 0; index < outcomes.Length - 1; index++)
        {
            cumulativeChance += chances[index];
            if (roll < cumulativeChance)
                return outcomes[index];
        }

        // The final configured reward is the fallback. This keeps every craft
        // atomic even when the XML rounds percentages or omits the final chance.
        return outcomes[^1];
    }

    private static LimitShopRewardOutcome[] GetOutcomes(LimitShopProductHolder product)
    {
        List<LimitShopRewardOutcome> outcomes =
        [
            new(0, product.getProductionId(), product.getCount(), product.getEnchant(), product.isAnnounce()),
        ];

        Add(outcomes, 1, product.getProductionId2(), product.getCount2(), product.isAnnounce2());
        Add(outcomes, 2, product.getProductionId3(), product.getCount3(), product.isAnnounce3());
        Add(outcomes, 3, product.getProductionId4(), product.getCount4(), product.isAnnounce4());
        Add(outcomes, 4, product.getProductionId5(), product.getCount5(), product.isAnnounce5());
        return outcomes.ToArray();
    }

    private static void Add(List<LimitShopRewardOutcome> outcomes, int index, int itemId, long count, bool announce)
    {
        if (itemId > 0)
            outcomes.Add(new LimitShopRewardOutcome(index, itemId, count, 0, announce));
    }
}
