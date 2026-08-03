using FluentAssertions;
using L2Dn.GameServer.Model.Holders;

namespace L2Dn.GameServer.StaticData.Tests;

public sealed class LimitShopRewardSelectorTests
{
    [Theory]
    [InlineData(0, 0, 97176, 1)]
    [InlineData(29.999, 0, 97176, 1)]
    [InlineData(30, 1, 92314, 6)]
    [InlineData(99.999, 1, 92314, 6)]
    public void Two_outcomes_use_one_weighted_roll(double roll, int index, int itemId, long count)
    {
        LimitShopProductHolder product = Product(
            productionId: 97176,
            chance: 30,
            productionId2: 92314,
            count2: 6,
            chance2: 70);

        LimitShopRewardSelector.Select(product, roll).Should()
            .Be(new LimitShopRewardOutcome(index, itemId, count, 0, false));
    }

    [Fact]
    public void Last_configured_outcome_is_the_fallback()
    {
        LimitShopProductHolder product = Product(
            productionId: 100,
            chance: 0.025f,
            productionId2: 200,
            chance2: 0.075f,
            productionId3: 300,
            chance3: 1.9f,
            productionId4: 400,
            chance4: 98);

        LimitShopRewardSelector.Select(product, 0.024).Should().Match<LimitShopRewardOutcome>(outcome => outcome.Index == 0);
        LimitShopRewardSelector.Select(product, 0.05).Should().Match<LimitShopRewardOutcome>(outcome => outcome.Index == 1);
        LimitShopRewardSelector.Select(product, 1.5).Should().Match<LimitShopRewardOutcome>(outcome => outcome.Index == 2);
        LimitShopRewardSelector.Select(product, 99.999).Should().Match<LimitShopRewardOutcome>(outcome => outcome.Index == 3);
    }

    [Fact]
    public void Single_outcome_preserves_enchant_and_can_fail()
    {
        LimitShopProductHolder product = Product(productionId: 98205, chance: 15, enchant: 5);

        LimitShopRewardSelector.Select(product, 14.999).Should()
            .Be(new LimitShopRewardOutcome(0, 98205, 1, 5, false));
        LimitShopRewardSelector.Select(product, 15).Should().BeNull();
    }

    private static LimitShopProductHolder Product(
        int productionId,
        float chance,
        int enchant = 0,
        int productionId2 = 0,
        long count2 = 1,
        float chance2 = 100,
        int productionId3 = 0,
        float chance3 = 100,
        int productionId4 = 0,
        float chance4 = 100) =>
        new(
            id: 1,
            category: 2,
            minLevel: 1,
            maxLevel: 999,
            ingredientIds: new int[5],
            ingredientQuantities: new long[5],
            ingredientEnchants: new int[5],
            productionId,
            count: 1,
            chance,
            announce: false,
            enchant,
            productionId2,
            count2,
            chance2,
            announce2: false,
            productionId3,
            count3: 1,
            chance3,
            announce3: false,
            productionId4,
            count4: 1,
            chance4,
            announce4: false,
            productionId5: 0,
            count5: 1,
            announce5: false,
            accountDailyLimit: 0,
            accountMontlyLimit: 0,
            accountBuyLimit: 0);
}
