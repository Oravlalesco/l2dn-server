using System.Globalization;
using System.Xml.Linq;
using L2Dn.Packages.DatDefinitions.Definitions;

namespace L2Dn.ClientDat;

internal sealed record SpecialCraftServerProduct(ushort ProductId, byte Category, short LevelMin, short LevelMax,
    IReadOnlyList<SpecialCraftServerOutcome> Outcomes);

internal sealed record SpecialCraftServerOutcome(uint ItemId, uint Count, float Probability, uint Enchant);

internal static class SpecialCraftCatalog
{
    private const byte specialCraftShopIndex = 4;

    public static IReadOnlyList<SpecialCraftServerProduct> ReadServerProducts(string path)
    {
        XDocument document = XDocument.Load(Path.GetFullPath(path), LoadOptions.SetLineInfo);
        XElement root = document.Root ?? throw new InvalidDataException("LimitShopCraft.xml has no root element.");
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
            int ingredientCount = product.Elements("ingredient").Count();
            if (ingredientCount is < 1 or > 5)
                throw new InvalidDataException($"Product {productId} must have between one and five ingredients.");

            result.Add(new SpecialCraftServerProduct(productId, category, levelMin, levelMax, outcomes));
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
        Dictionary<ushort, PurchaseLimitCraftV7.PurchaseLimitCraftRecord> donorRecords = donorData.Records
            .Where(record => record.ShopIndex == specialCraftShopIndex)
            .ToDictionary(record => record.ProductId);
        List<PurchaseLimitCraftV7.PurchaseLimitCraftRecord> selectedRecords = new(serverProducts.Count);
        foreach (SpecialCraftServerProduct product in serverProducts)
        {
            if (!donorRecords.TryGetValue(product.ProductId, out PurchaseLimitCraftV7.PurchaseLimitCraftRecord? record))
                throw new InvalidDataException($"Product {product.ProductId} is missing from the ClassicAden donor DAT.");

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

    private static uint ParseUInt(XAttribute attribute) =>
        uint.Parse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture);

    private static float ParseFloat(XAttribute attribute) =>
        float.Parse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture);
}
