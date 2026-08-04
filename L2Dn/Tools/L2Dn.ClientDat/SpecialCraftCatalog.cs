using System.Globalization;
using System.Xml.Linq;
using L2Dn.Packages.DatDefinitions.Definitions;
using L2Dn.Packages.DatDefinitions.Definitions.Enums;

namespace L2Dn.ClientDat;

internal sealed record SpecialCraftServerProduct(ushort ProductId, byte Category, short LevelMin, short LevelMax,
    string ProductName, IReadOnlyList<SpecialCraftServerIngredient> Ingredients,
    IReadOnlyList<SpecialCraftServerOutcome> Outcomes);

internal sealed record SpecialCraftServerIngredient(uint ItemId, long Count, uint Enchant);

internal sealed record SpecialCraftServerOutcome(uint ItemId, uint Count, float Probability, uint Enchant);

internal static class SpecialCraftCatalog
{
    private const byte specialCraftShopIndex = 4;

    public static IReadOnlyList<SpecialCraftServerProduct> ReadServerProducts(string path)
    {
        string fullPath = Path.GetFullPath(path);
        XDocument document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
        XElement root = document.Root ?? throw new InvalidDataException("LimitShopCraft.xml has no root element.");
        IReadOnlyDictionary<uint, string> itemNames = ReadServerItemNames(fullPath);
        List<SpecialCraftServerProduct> result = new();
        foreach (XElement product in root.Elements("product"))
        {
            ushort productId = checked((ushort)GetUInt(product, "id"));
            byte category = checked((byte)GetUInt(product, "category"));
            short levelMin = checked((short)GetInt(product, "minLevel", 1));
            short levelMax = checked((short)GetInt(product, "maxLevel", 999));
            XElement production = product.Element("production")
                ?? throw new InvalidDataException($"Product {productId} has no production element.");
            IReadOnlyList<SpecialCraftServerOutcome> outcomes = ReadOutcomes(productId, production);
            SpecialCraftServerOutcome primaryOutcome = outcomes[0];
            if (!itemNames.TryGetValue(primaryOutcome.ItemId, out string? itemName))
                throw new InvalidDataException($"Product {productId} references unnamed item {primaryOutcome.ItemId}.");

            string productName = primaryOutcome.Enchant > 0
                ? $"+{primaryOutcome.Enchant} {itemName}"
                : itemName;
            SpecialCraftServerIngredient[] ingredients = product.Elements("ingredient")
                .Select(ingredient => new SpecialCraftServerIngredient(
                    GetUInt(ingredient, "id"),
                    GetLong(ingredient, "count", 1),
                    GetUInt(ingredient, "enchant")))
                .ToArray();
            if (ingredients.Length is < 1 or > 5)
                throw new InvalidDataException($"Product {productId} must have between one and five ingredients.");

            result.Add(new SpecialCraftServerProduct(productId, category, levelMin, levelMax, productName,
                ingredients, outcomes));
        }

        ushort[] duplicates = result.GroupBy(product => product.ProductId).Where(group => group.Count() > 1)
            .Select(group => group.Key).ToArray();
        if (duplicates.Length != 0)
            throw new InvalidDataException($"Duplicate server ProductId values: {string.Join(", ", duplicates)}");

        return result;
    }

    public static PurchaseLimitCraftV7 Build(PurchaseLimitCraftV7 baseData, PurchaseLimitCraftV7 donorData,
        NpcString npcStrings, IReadOnlyList<SpecialCraftServerProduct> serverProducts)
    {
        Dictionary<ushort, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> baseRecords = baseData.Records
            .Where(record => record.ShopIndex == specialCraftShopIndex)
            .ToDictionary(record => record.ProductId);
        Dictionary<ushort, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> donorRecords = donorData.Records
            .Where(record => record.ShopIndex == specialCraftShopIndex)
            .ToDictionary(record => record.ProductId);
        ushort[] unknownProductIds = serverProducts.Select(product => product.ProductId)
            .Where(productId => !baseRecords.ContainsKey(productId) && !donorRecords.ContainsKey(productId))
            .Order()
            .ToArray();
        if (unknownProductIds.Length != 0)
        {
            throw new InvalidDataException(
                $"ProductId values absent from the Classic/Classic Aden client: {string.Join(", ", unknownProductIds)}");
        }

        Dictionary<byte, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> categoryTemplates = BuildCategoryTemplates(
            baseRecords, donorRecords);
        List<PurchaseLimitCraftV7.PurchaseLimitCraftRecord> selectedRecords = new(serverProducts.Count);
        foreach (SpecialCraftServerProduct product in serverProducts)
        {
            PurchaseLimitCraftV7.PurchaseLimitCraftRecord template;
            if (baseRecords.TryGetValue(product.ProductId, out PurchaseLimitCraftV7.PurchaseLimitCraftRecord? baseRecord) &&
                MatchesProduct(baseRecord, product))
            {
                template = baseRecord;
            }
            else if (donorRecords.TryGetValue(product.ProductId,
                         out PurchaseLimitCraftV7.PurchaseLimitCraftRecord? donorRecord) &&
                     MatchesProduct(donorRecord, product))
            {
                template = donorRecord;
            }
            else if (!categoryTemplates.TryGetValue(product.Category, out template!))
            {
                throw new InvalidDataException($"No client template exists for Special Craft category {product.Category}.");
            }

            PurchaseLimitCraftV7.PurchaseLimitCraftRecord record = CreateRecord(template, product);
            VerifyRecord(record, product);
            selectedRecords.Add(record);
        }

        PurchaseLimitCraftV7 result = new()
        {
            Records = baseData.Records.Where(record => record.ShopIndex != specialCraftShopIndex)
                .Concat(selectedRecords).ToArray(),
        };
        Verify(result, npcStrings, serverProducts);
        return result;
    }

    public static void Verify(PurchaseLimitCraftV7 data, NpcString npcStrings,
        IReadOnlyList<SpecialCraftServerProduct> serverProducts)
    {
        PurchaseLimitCraftV7.PurchaseLimitCraftRecord[] records = data.Records
            .Where(record => record.ShopIndex == specialCraftShopIndex).ToArray();
        Dictionary<ushort, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> recordsById;
        try
        {
            recordsById = records.ToDictionary(record => record.ProductId);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException("The client DAT contains duplicate Special Craft ProductId values.", exception);
        }

        HashSet<ushort> serverIds = serverProducts.Select(product => product.ProductId).ToHashSet();
        ushort[] extraIds = recordsById.Keys.Where(productId => !serverIds.Contains(productId)).Order().ToArray();
        if (extraIds.Length != 0)
            throw new InvalidDataException($"Client-only Special Craft ProductId values: {string.Join(", ", extraIds)}");

        HashSet<uint> npcStringIds = npcStrings.Records.Select(record => record.Id).ToHashSet();
        foreach (SpecialCraftServerProduct product in serverProducts)
        {
            if (!recordsById.TryGetValue(product.ProductId, out PurchaseLimitCraftV7.PurchaseLimitCraftRecord? record))
                throw new InvalidDataException($"Server ProductId {product.ProductId} is missing from the client DAT.");

            VerifyRecord(record, product);
            if (!string.Equals(record.ProductName, product.ProductName, StringComparison.Ordinal))
                throw new InvalidDataException($"Product {product.ProductId} has a different client/server name.");
            if (record.KeepOption != 0 || record.KeepOptionFees.Length != 0)
                throw new InvalidDataException($"Product {product.ProductId} must not advertise unsupported option succession.");
            if (!npcStringIds.Contains(record.CategorySub))
                throw new InvalidDataException($"Product {product.ProductId} references missing NpcString {record.CategorySub}.");
        }
    }

    private static void VerifyRecord(PurchaseLimitCraftV7.PurchaseLimitCraftRecord record,
        SpecialCraftServerProduct serverProduct)
    {
        if (record.ShopIndex != specialCraftShopIndex)
            throw new InvalidDataException($"Product {record.ProductId} has ShopIndex {record.ShopIndex}, expected 4.");
        if (record.Category != serverProduct.Category)
            throw new InvalidDataException($"Product {record.ProductId} has client category {record.Category} and server category {serverProduct.Category}.");
        if (record.LevelMin != serverProduct.LevelMin || record.LevelMax != serverProduct.LevelMax)
            throw new InvalidDataException($"Product {record.ProductId} has a different client/server level range.");
        if (record.BuyItems.Length != serverProduct.Outcomes.Count)
            throw new InvalidDataException($"Product {record.ProductId} has a different number of client/server outcomes.");
        if (record.ProductItem != serverProduct.Outcomes[0].ItemId ||
            record.ProductEnchant != serverProduct.Outcomes[0].Enchant)
            throw new InvalidDataException($"Product {record.ProductId} has a different primary client/server outcome.");

        for (int index = 0; index < record.BuyItems.Length; index++)
        {
            PurchaseLimitCraftV7.PurchaseLimitCraftBuyItem clientOutcome = record.BuyItems[index];
            SpecialCraftServerOutcome serverOutcome = serverProduct.Outcomes[index];
            if (clientOutcome.ItemClassId != serverOutcome.ItemId || clientOutcome.Count != serverOutcome.Count ||
                clientOutcome.Enchant != serverOutcome.Enchant ||
                Math.Abs(clientOutcome.Probability - serverOutcome.Probability) > 0.001f)
            {
                throw new InvalidDataException($"Product {record.ProductId}, outcome {index + 1}, differs between client and server.");
            }
        }
    }

    private static IReadOnlyList<SpecialCraftServerOutcome> ReadOutcomes(ushort productId, XElement production)
    {
        List<SpecialCraftServerOutcome> outcomes = new();
        bool hasAlternatives = Enumerable.Range(2, 4).Any(index => production.Attribute($"id{index}") is not null);
        for (int index = 1; index <= 5; index++)
        {
            string suffix = index == 1 ? string.Empty : index.ToString(CultureInfo.InvariantCulture);
            XAttribute? idAttribute = production.Attribute($"id{suffix}");
            if (idAttribute is null)
                continue;

            uint itemId = ParseUInt(idAttribute);
            uint count = GetUInt(production, $"count{suffix}", 1);
            uint enchant = index == 1 ? GetUInt(production, "enchant", 0) : 0;
            XAttribute? chanceAttribute = production.Attribute($"chance{suffix}");
            float chance;
            if (chanceAttribute is not null)
            {
                chance = ParseFloat(chanceAttribute);
            }
            else if (index == 1 && !hasAlternatives)
            {
                chance = 100;
            }
            else if (index == 5)
            {
                chance = 100 - outcomes.Sum(outcome => outcome.Probability);
            }
            else
            {
                throw new InvalidDataException($"Product {productId}, outcome {index}, has no chance value.");
            }

            outcomes.Add(new SpecialCraftServerOutcome(itemId, count, chance, enchant));
        }

        if (outcomes.Count == 0)
            throw new InvalidDataException($"Product {productId} has no outcomes.");
        if (Math.Abs(outcomes.Sum(outcome => outcome.Probability) - 100) > 0.001f)
            throw new InvalidDataException($"Product {productId} outcome chances do not total 100%.");
        return outcomes;
    }

    private static Dictionary<byte, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> BuildCategoryTemplates(
        IReadOnlyDictionary<ushort, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> baseRecords,
        IReadOnlyDictionary<ushort, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> donorRecords)
    {
        Dictionary<byte, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> result = new();
        AddTemplate(result, baseRecords, donorRecords, 1542, 0); // Frost Lord weapons.
        AddTemplate(result, baseRecords, donorRecords, 4238, 2); // Spellbooks.
        AddTemplate(result, baseRecords, donorRecords, 4322, 3); // Accessories.
        AddTemplate(result, baseRecords, donorRecords, 1112, 4); // Scrolls and general crafting.
        AddTemplate(result, baseRecords, donorRecords, 1203, 5); // Blessings.
        return result;
    }

    private static bool MatchesProduct(PurchaseLimitCraftV7.PurchaseLimitCraftRecord record,
        SpecialCraftServerProduct product) =>
        record.Category == product.Category &&
        record.ProductItem == product.Outcomes[0].ItemId &&
        record.ProductEnchant == product.Outcomes[0].Enchant;

    private static void AddTemplate(Dictionary<byte, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> templates,
        IReadOnlyDictionary<ushort, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> primaryRecords,
        IReadOnlyDictionary<ushort, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> secondaryRecords,
        ushort productId, byte category)
    {
        if ((!primaryRecords.TryGetValue(productId, out PurchaseLimitCraftV7.PurchaseLimitCraftRecord? record) ||
             record.Category != category) &&
            (!secondaryRecords.TryGetValue(productId, out record) || record.Category != category))
        {
            record = primaryRecords.Values
                .Concat(secondaryRecords.Values)
                .Where(candidate => candidate.Category == category)
                .OrderBy(candidate => candidate.ProductId)
                .FirstOrDefault()
                ?? throw new InvalidDataException($"No client template exists for Special Craft category {category}.");
        }

        templates.Add(category, record);
    }

    private static PurchaseLimitCraftV7.PurchaseLimitCraftRecord CreateRecord(
        PurchaseLimitCraftV7.PurchaseLimitCraftRecord template, SpecialCraftServerProduct product)
    {
        uint primaryRank = template.BuyItems.FirstOrDefault()?.ProductRank ?? 0;
        PurchaseLimitCraftV7.PurchaseLimitCraftBuyItem[] outcomes = product.Outcomes
            .Select((outcome, index) => new PurchaseLimitCraftV7.PurchaseLimitCraftBuyItem
            {
                ItemClassId = outcome.ItemId,
                Count = outcome.Count,
                Probability = outcome.Probability,
                Enchant = outcome.Enchant,
                ProductRank = index == 0 ? primaryRank : 0,
                IsLimitServer = 0,
            })
            .ToArray();

        return new PurchaseLimitCraftV7.PurchaseLimitCraftRecord
        {
            ShopIndex = specialCraftShopIndex,
            ProductId = product.ProductId,
            Category = product.Category,
            CategorySub = template.CategorySub,
            MarkType = template.MarkType,
            MaxBuyCount = template.MaxBuyCount,
            ProductName = product.ProductName,
            ProductItem = product.Outcomes[0].ItemId,
            ProductEnchant = product.Outcomes[0].Enchant,
            BuyItems = outcomes,
            LevelMin = product.LevelMin,
            LevelMax = product.LevelMax,
            LimitType = 0,
            ResetType = LCoinResetType.Always,
            LimitServerBuyCountMax = 0,
            RequirementBuySkills = Array.Empty<uint>(),
            KeepOptionFees = Array.Empty<PurchaseLimitCraftV7.PurchaseLimitCraftKeepOptionFee>(),
            KeepOption = 0,
            AutomaticType = 0,
        };
    }

    public static IReadOnlyDictionary<uint, string> ReadServerItemNames(string catalogPath)
    {
        string dataPackDirectory = Path.GetDirectoryName(catalogPath)
            ?? throw new InvalidDataException("LimitShopCraft.xml has no parent directory.");
        string itemDirectory = Path.Combine(dataPackDirectory, "stats", "items");
        if (!Directory.Exists(itemDirectory))
            throw new DirectoryNotFoundException($"Server item directory not found: {itemDirectory}");

        Dictionary<uint, string> names = new();
        foreach (string itemFile in Directory.EnumerateFiles(itemDirectory, "*.xml"))
        {
            XElement? root = XDocument.Load(itemFile).Root;
            if (root == null)
                continue;

            foreach (XElement item in root.Elements("item"))
            {
                XAttribute? id = item.Attribute("id");
                XAttribute? name = item.Attribute("name");
                if (id == null || name == null)
                    continue;

                names[ParseUInt(id)] = name.Value;
            }
        }

        return names;
    }

    private static int GetInt(XElement element, string name, int defaultValue)
    {
        XAttribute? attribute = element.Attribute(name);
        return attribute is null ? defaultValue : int.Parse(attribute.Value, CultureInfo.InvariantCulture);
    }

    private static uint GetUInt(XElement element, string name, uint defaultValue = 0)
    {
        XAttribute? attribute = element.Attribute(name);
        if (attribute is null)
            return defaultValue;
        return ParseUInt(attribute);
    }

    private static long GetLong(XElement element, string name, long defaultValue = 0)
    {
        XAttribute? attribute = element.Attribute(name);
        return attribute is null
            ? defaultValue
            : long.Parse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture);
    }

    private static uint ParseUInt(XAttribute attribute) =>
        uint.Parse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture);

    private static float ParseFloat(XAttribute attribute) =>
        float.Parse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
}
