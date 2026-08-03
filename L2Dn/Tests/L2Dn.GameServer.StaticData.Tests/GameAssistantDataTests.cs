using System.Xml.Linq;
using FluentAssertions;

namespace L2Dn.GameServer.StaticData.Tests;

public sealed class GameAssistantDataTests
{
    private static readonly (int ItemId, int ListId, bool Enabled)[] ExpectedRoutes =
    [
        (90907, 20000, true),
        (49487, 20001, true),
        (98206, 20002, false),
        (98044, 20003, false),
        (98440, 20004, false),
        (98441, 20005, false),
        (98443, 20006, false),
        (99512, 20007, false),
        (99511, 20008, false),
    ];

    private static readonly (int ProductId, long Count)[] ExpectedProducts =
    [
        (91927, 500),
        (91928, 334),
        (91929, 200),
        (91930, 134),
    ];

    [Fact]
    public void Item_multisell_registry_has_the_verified_routes_and_only_verified_routes_are_enabled()
    {
        XElement[] routes = XDocument.Load(DataPackPath("ItemMultisells.xml"))
            .Root!
            .Elements("route")
            .ToArray();

        routes.Select(route =>
                ((int)route.Attribute("itemId")!,
                    (int)route.Attribute("listId")!,
                    (bool)route.Attribute("enabled")!))
            .Should()
            .Equal(ExpectedRoutes);

        routes.Select(route => (int)route.Attribute("itemId")!).Should().OnlyHaveUniqueItems();
        routes.Select(route => (int)route.Attribute("listId")!).Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(20000, 90907)]
    [InlineData(20001, 49487)]
    public void Enabled_ticket_catalogs_consume_one_ticket_for_the_verified_products(int listId, int ticketId)
    {
        XElement root = XDocument.Load(DataPackPath("multisell", "items", $"{listId}.xml")).Root!;

        root.Element("npcs")!.Elements("npc").Select(npc => (int)npc).Should().Equal(-1);
        XElement[] entries = root.Elements("item").ToArray();
        entries.Should().HaveCount(ExpectedProducts.Length);

        entries.Select(entry => entry.Elements("ingredient").Single())
            .Select(ingredient => ((int)ingredient.Attribute("id")!, (long)ingredient.Attribute("count")!))
            .Should()
            .Equal(Enumerable.Repeat((ticketId, 1L), ExpectedProducts.Length));

        entries.Select(entry => entry.Elements("production").Single())
            .Select(product => ((int)product.Attribute("id")!, (long)product.Attribute("count")!))
            .Should()
            .Equal(ExpectedProducts);
    }

    [Theory]
    [InlineData("32478.html")]
    [InlineData("32478-button.html")]
    public void Main_game_assistant_pages_expose_all_four_services(string fileName)
    {
        string html = File.ReadAllText(DataPackPath("scripts", "ai", "others", "GameAssistant", fileName));

        html.Should().Contain("game_assistant events");
        html.Should().Contain("game_assistant premium");
        html.Should().Contain("package_deposit");
        html.Should().Contain("package_withdraw");
    }

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
}
