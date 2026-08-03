using System.Xml.Linq;
using FluentAssertions;
using L2Dn.GameServer.Model.Variables;

namespace L2Dn.GameServer.StaticData.Tests;

public sealed class ClassicSpecialCraftCatalogTests
{
    private static readonly CatalogProduct[] ExpectedProducts =
    [
        new(10001, 0, 91663, 1000, 92020),
        new(10002, 0, 91663, 800, 91767),
        new(10060, 0, 92314, 50, 94214),

        new(10021, 1, 91663, 800, 91032),
        new(10022, 1, 91663, 800, 91033),
        new(10023, 1, 91663, 800, 91034),
        new(10024, 1, 91663, 800, 91031),
        new(10061, 1, 92314, 80, 22223),
        new(10062, 1, 92314, 40, 49486),
        new(10063, 1, 92314, 40, 22224),
        new(10064, 1, 92314, 20, 49485),

        new(10012, 2, 91663, 300, 90404),
        new(10013, 2, 91663, 20, 90405),
        new(10014, 2, 91663, 20, 91689),
        new(10042, 2, 91663, 50, 90519),
        new(10043, 2, 91663, 100, 90183),
        new(10044, 2, 91663, 350, 49081),
        new(10011, 2, 91663, 20, 91641),

        new(10018, 3, 92314, 10, 92006),
        new(10019, 3, 92314, 20, 92007),
        new(10020, 3, 92314, 40, 92008),
        new(10025, 3, 57, 5000000, 32253),
        new(10026, 3, 57, 3000000, 32252),
        new(10027, 3, 57, 2000000, 32251),
        new(10028, 3, 57, 1000000, 32250),
        new(10065, 3, 92314, 100, 32254),

        new(10016, 4, 92314, 2, 92004),
        new(10017, 4, 92314, 5, 92005),
        new(10035, 4, 91663, 20, 91641, DailyLimit: 20),
        new(10066, 4, 91663, 1000, 92020, DailyLimit: 1),
        new(10067, 4, 91663, 800, 91767, DailyLimit: 1),
        new(10068, 4, 92314, 25, 94208, DailyLimit: 1),
        new(10071, 4, 91663, 100, 94377, DailyLimit: 10),
        new(10073, 4, 57, 5000000, 94781, BuyLimit: 2),
        new(10074, 4, 57, 1000000, 94780, DailyLimit: 100),
        new(10124, 4, 92314, 15, 93406, DailyLimit: 5),
        new(10125, 4, 92314, 10, 93407, DailyLimit: 5),
        new(10126, 4, 92314, 8, 93408, DailyLimit: 5),
        new(10127, 4, 92314, 5, 93409, DailyLimit: 5),
        new(10128, 4, 92314, 3, 93410, DailyLimit: 5),
        new(10129, 4, 92314, 2, 93411, DailyLimit: 5),
    ];

    [Fact]
    public void Catalog_matches_the_active_classic_client_product_manifest()
    {
        CatalogProduct[] products = LoadProducts();

        products.Should().Equal(ExpectedProducts);
        products.Select(product => product.ProductId).Should().OnlyHaveUniqueItems();
        products.Should().NotContain(product => product.ProductionId == 99041 || product.ProductionId == 99042);
    }

    [Fact]
    public void Catalog_populates_every_supported_tab_and_reserves_event_for_a_client_patch()
    {
        LoadProducts()
            .GroupBy(product => product.Category)
            .ToDictionary(group => group.Key, group => group.Count())
            .Should()
            .BeEquivalentTo(new Dictionary<int, int>
            {
                [0] = 3,
                [1] = 8,
                [2] = 7,
                [3] = 8,
                [4] = 15,
            });
    }

    [Fact]
    public void Every_catalog_item_exists_in_the_server_datapack()
    {
        HashSet<int> itemIds = Directory
            .EnumerateFiles(DataPackPath("stats", "items"), "*.xml")
            .SelectMany(path => XDocument.Load(path).Descendants("item"))
            .Select(item => (int)item.Attribute("id")!)
            .ToHashSet();

        ExpectedProducts
            .SelectMany(product => new[] { product.IngredientId, product.ProductionId })
            .Distinct()
            .Should()
            .OnlyContain(itemId => itemIds.Contains(itemId));
    }

    [Fact]
    public void Account_limit_keys_are_scoped_by_shop_and_product()
    {
        AccountVariables.getLCoinShopProductDailyCountName(4, 10035).Should().Be("LCSDailyCount4_10035");
        AccountVariables.getLCoinShopProductDailyCountName(3, 10035)
            .Should().NotBe(AccountVariables.getLCoinShopProductDailyCountName(4, 10035));
        AccountVariables.getLCoinShopProductDailyCountName(4, 10066)
            .Should().NotBe(AccountVariables.getLCoinShopProductDailyCountName(4, 10067));
    }

    private static CatalogProduct[] LoadProducts() => XDocument
        .Load(DataPackPath("LimitShopCraft.xml"))
        .Root!
        .Elements("product")
        .Select(product =>
        {
            XElement ingredient = product.Elements("ingredient").Single();
            XElement production = product.Elements("production").Single();
            return new CatalogProduct(
                (int)product.Attribute("id")!,
                (int)product.Attribute("category")!,
                (int)ingredient.Attribute("id")!,
                (long)ingredient.Attribute("count")!,
                (int)production.Attribute("id")!,
                (int?)production.Attribute("accountDailyLimit") ?? 0,
                (int?)production.Attribute("accountBuyLimit") ?? 0);
        })
        .ToArray();

    private static string DataPackPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string dataPack = Path.Combine(directory.FullName, "L2Dn.GameServer", "DataPack");
            if (Directory.Exists(dataPack))
                return Path.Combine([dataPack, .. segments]);

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GameServer DataPack from the test output directory.");
    }

    private sealed record CatalogProduct(
        int ProductId,
        int Category,
        int IngredientId,
        long IngredientCount,
        int ProductionId,
        int DailyLimit = 0,
        int BuyLimit = 0);
}
