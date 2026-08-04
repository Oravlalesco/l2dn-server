using System.Text.Json;
using System.Text.Json.Serialization;
using L2Dn.Packages.DatDefinitions;
using L2Dn.Packages.DatDefinitions.Definitions;

namespace L2Dn.ClientDat;

internal static class Commands
{
    private static readonly JsonSerializerOptions _jsonOptions = CreateJsonOptions();

    public static int Inspect(string[] args)
    {
        if (args.Length is < 2 or > 3)
            return Usage();

        PurchaseLimitCraftV7 data = ClientDatFile.Read<PurchaseLimitCraftV7>(args[1], out string rsaKey);
        string json = JsonSerializer.Serialize(data, _jsonOptions);
        if (args.Length == 3)
        {
            string outputPath = Path.GetFullPath(args[2]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, json);
            Console.WriteLine($"Wrote {data.Records.Length} records to {outputPath} (RSA key: {rsaKey})");
        }
        else
        {
            Console.WriteLine(json);
        }

        return 0;
    }

    public static int BuildSpecialCraft(string[] args)
    {
        if (args.Length != 7)
            return Usage();

        PurchaseLimitCraftV7 baseData = ClientDatFile.Read<PurchaseLimitCraftV7>(args[1], out string baseKey);
        PurchaseLimitCraftV7 donorData = ClientDatFile.Read<PurchaseLimitCraftV7>(args[2], out string donorKey);
        NpcString npcStrings = ClientDatFile.Read<NpcString>(args[3], out string npcStringKey);
        IReadOnlyList<SpecialCraftServerProduct> serverProducts = SpecialCraftCatalog.ReadServerProducts(args[4]);
        PurchaseLimitCraftV7 outputData = SpecialCraftCatalog.Build(baseData, donorData, npcStrings, serverProducts);

        string outputPath = Path.GetFullPath(args[5]);
        string manifestPath = Path.GetFullPath(args[6]);
        ClientDatFile.WriteLineage2Ver413(outputPath, outputData);
        WriteManifest(manifestPath, outputData);

        PurchaseLimitCraftV7 verificationData = ClientDatFile.Read<PurchaseLimitCraftV7>(outputPath, out string outputKey);
        SpecialCraftCatalog.Verify(verificationData, npcStrings, serverProducts);

        Console.WriteLine($"Base: {baseData.Records.Length} records ({baseKey})");
        Console.WriteLine($"Donor: {donorData.Records.Length} records ({donorKey})");
        Console.WriteLine($"NpcString: {npcStrings.Records.Length} records ({npcStringKey})");
        Console.WriteLine($"Output: {verificationData.Records.Length} records ({outputKey}) -> {outputPath}");
        Console.WriteLine($"Manifest: {manifestPath}");
        return 0;
    }

    public static int BuildSpecialCraftBundle(string[] args)
    {
        if (args.Length != 4)
            return Usage();

        string outputDirectory = Path.GetFullPath(args[3]);
        SpecialCraftClientBundleResult result = SpecialCraftClientBundle.Build(args[1], args[2], outputDirectory);
        WriteManifest(Path.Combine(outputDirectory, "PurchaseLimitCraft_Classic-eu.json"), result.Craft);

        Console.WriteLine($"Bundle: {result.Craft.Records.Count(record => record.ShopIndex == 4)} recipes, " +
                          $"{result.ReferencedItems} referenced items");
        Console.WriteLine($"Imported from Classic Aden: names={result.AddedItemNames}, " +
                          $"etc={result.AddedEtcItems}, armor={result.AddedArmorItems}, " +
                          $"weapon={result.AddedWeaponItems}, baseInfo={result.AddedBaseInfoItems}, " +
                          $"additional={result.AddedAdditionalItems}, stats={result.AddedItemStats}, " +
                          $"gameDataNames={result.AddedGameDataNames}, reindexedNames={result.ReindexedItemNames}");
        Console.WriteLine($"Output: {outputDirectory}");
        return 0;
    }

    public static int AuditSpecialCraftClient(string[] args)
    {
        if (args.Length is < 9 or > 10)
            return Usage();

        L2NameData nameData = ClientDatFile.Read<L2NameData>(args[4], out string nameDataKey);
        DatReader.SetNameData(nameData.Names);
        PurchaseLimitCraftV7 craft = ClientDatFile.Read<PurchaseLimitCraftV7>(args[1], out string craftKey);
        PurchaseLimitCraftCategory categories = ClientDatFile.Read<PurchaseLimitCraftCategory>(args[2],
            out string categoryKey);
        NpcString npcStrings = ClientDatFile.Read<NpcString>(args[3], out string npcStringKey);
        Dictionary<uint, string> npcStringsById = npcStrings.Records.GroupBy(record => record.Id)
            .ToDictionary(group => group.Key, group => group.First().String);
        ItemNameV18 itemNames = ClientDatFile.Read<ItemNameV18>(args[5], out string itemNameKey);
        EtcItemGrpV9 etcItems = ClientDatFile.Read<EtcItemGrpV9>(args[6], out string etcItemKey);
        ArmorGrpV14 armorItems = ClientDatFile.Read<ArmorGrpV14>(args[7], out string armorKey);
        WeaponGrpV12 weaponItems = ClientDatFile.Read<WeaponGrpV12>(args[8], out string weaponKey);

        Dictionary<uint, ItemNameV18.ItemNameRecord> namesById = itemNames.Records
            .GroupBy(record => record.Id)
            .ToDictionary(group => group.Key, group => group.First());
        Dictionary<uint, AssetDescription> assetsById = new();
        foreach (EtcItemGrpV9.EtcItemGrpRecord record in etcItems.Records)
            assetsById.TryAdd(record.ObjectId, new AssetDescription("EtcItemGrp", record.Icons.Select(IconText).ToArray()));
        foreach (ArmorGrpV14.ArmorGrpRecord record in armorItems.Records)
            assetsById.TryAdd(record.ObjectId, new AssetDescription("ArmorGrp", record.Icons.Select(IconText).ToArray()));
        foreach (WeaponGrpV12.WeaponGrpRecord record in weaponItems.Records)
            assetsById.TryAdd(record.ObjectId, new AssetDescription("WeaponGrp", record.Icons.Select(IconText).ToArray()));

        var records = craft.Records.Where(record => record.ShopIndex == 4).Select(record =>
        {
            namesById.TryGetValue(record.ProductItem, out ItemNameV18.ItemNameRecord? itemName);
            assetsById.TryGetValue(record.ProductItem, out AssetDescription? asset);
            return new
            {
                record.ProductId,
                record.Category,
                record.CategorySub,
                CategorySubName = npcStringsById.GetValueOrDefault(record.CategorySub, string.Empty),
                record.ProductName,
                record.ProductItem,
                ClientItemName = itemName?.Name.Text ?? string.Empty,
                HasItemName = itemName != null,
                AssetTable = asset?.Table ?? string.Empty,
                Icons = asset?.Icons ?? [],
                HasVisualAsset = asset != null && asset.Icons.Any(icon => !string.IsNullOrWhiteSpace(icon)),
            };
        }).ToArray();

        var report = new
        {
            Categories = categories.Records.Select(record => new
            {
                record.CategoryId,
                record.StringId,
                Name = npcStringsById.GetValueOrDefault(record.StringId, string.Empty),
                Color = new { record.StringColor.R, record.StringColor.G, record.StringColor.B, record.StringColor.A },
                record.RibbonTextures,
                record.Optional,
            }).ToArray(),
            Records = records,
        };

        Console.WriteLine($"Craft: {records.Length} records ({craftKey})");
        Console.WriteLine($"Categories: {categories.Records.Length} records ({categoryKey}) -> " +
                          string.Join(", ", categories.Records.Select(record => $"{record.CategoryId}:{record.StringId}")));
        Console.WriteLine($"NpcString: {npcStrings.Records.Length} ({npcStringKey}); item names: {itemNames.Records.Length} ({itemNameKey}); name data: {nameData.Names.Length} ({nameDataKey})");
        Console.WriteLine($"Assets: etc={etcItems.Records.Length} ({etcItemKey}), armor={armorItems.Records.Length} ({armorKey}), weapon={weaponItems.Records.Length} ({weaponKey})");
        Console.WriteLine($"ID ranges: names={IdRange(itemNames.Records.Select(record => record.Id))}, " +
                          $"etc={IdRange(etcItems.Records.Select(record => record.ObjectId))}, " +
                          $"armor={IdRange(armorItems.Records.Select(record => record.ObjectId))}, " +
                          $"weapon={IdRange(weaponItems.Records.Select(record => record.ObjectId))}");
        Console.WriteLine($"Missing item names: {records.Count(record => !record.HasItemName)}");
        Console.WriteLine($"Missing visual assets: {records.Count(record => !record.HasVisualAsset)}");

        if (args.Length == 10)
        {
            string outputPath = Path.GetFullPath(args[9]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(report, _jsonOptions));
            Console.WriteLine($"Audit: {outputPath}");
        }

        return 0;
    }

    public static int AuditSpecialCraftSupport(string[] args)
    {
        if (args.Length is < 3 or > 4)
            return Usage();

        string clientSystem = Path.GetFullPath(args[1]);
        string clientEu = Path.Combine(clientSystem, "eu");
        L2NameData nameData = ClientDatFile.Read<L2NameData>(Path.Combine(clientEu, "L2GameDataName.dat"), out _);
        DatReader.SetNameData(nameData.Names);

        IReadOnlyList<SpecialCraftServerProduct> products = SpecialCraftCatalog.ReadServerProducts(args[2]);
        uint[] requiredIds = products
            .SelectMany(product => product.Ingredients.Select(item => item.ItemId)
                .Concat(product.Outcomes.Select(item => item.ItemId)))
            .Distinct()
            .Order()
            .ToArray();

        ItemBaseInfoV5 baseInfo = ClientDatFile.Read<ItemBaseInfoV5>(
            Path.Combine(clientEu, "item_baseinfo_Classic.dat"), out string baseInfoKey);
        ItemBaseInfoV5 donorInfo = ClientDatFile.Read<ItemBaseInfoV5>(
            Path.Combine(clientEu, "item_baseinfo_ClassicAden.dat"), out string donorInfoKey);
        AdditionalItemGrpV4 baseAdditional = ClientDatFile.Read<AdditionalItemGrpV4>(
            Path.Combine(clientEu, "AdditionalItemGrp_Classic.dat"), out string baseAdditionalKey);
        AdditionalItemGrpV4 donorAdditional = ClientDatFile.Read<AdditionalItemGrpV4>(
            Path.Combine(clientEu, "AdditionalItemGrp_ClassicAden.dat"), out string donorAdditionalKey);
        ItemStatDataV4 baseStats = ClientDatFile.Read<ItemStatDataV4>(
            Path.Combine(clientEu, "ItemStatData_Classic.dat"), out string baseStatsKey);
        ItemStatDataV4 donorStats = ClientDatFile.Read<ItemStatDataV4>(
            Path.Combine(clientEu, "ItemStatData_ClassicAden.dat"), out string donorStatsKey);

        HashSet<uint> baseInfoIds = baseInfo.Records.Select(record => record.ItemId).ToHashSet();
        HashSet<uint> donorInfoIds = donorInfo.Records.Select(record => record.ItemId).ToHashSet();
        HashSet<uint> baseAdditionalIds = baseAdditional.Records.Select(record => record.Id).ToHashSet();
        HashSet<uint> donorAdditionalIds = donorAdditional.Records.Select(record => record.Id).ToHashSet();
        HashSet<uint> baseStatIds = baseStats.Records.Select(record => record.ItemId).ToHashSet();
        HashSet<uint> donorStatIds = donorStats.Records.Select(record => record.ItemId).ToHashSet();

        var records = requiredIds.Select(itemId => new
        {
            ItemId = itemId,
            BaseInfoClassic = baseInfoIds.Contains(itemId),
            BaseInfoClassicAden = donorInfoIds.Contains(itemId),
            AdditionalClassic = baseAdditionalIds.Contains(itemId),
            AdditionalClassicAden = donorAdditionalIds.Contains(itemId),
            StatsClassic = baseStatIds.Contains(itemId),
            StatsClassicAden = donorStatIds.Contains(itemId),
        }).ToArray();

        Console.WriteLine($"Referenced items: {requiredIds.Length}");
        WriteSupportSummary("item_baseinfo", records.Count(record => record.BaseInfoClassic),
            records.Count(record => !record.BaseInfoClassic && record.BaseInfoClassicAden),
            records.Where(record => !record.BaseInfoClassic && !record.BaseInfoClassicAden).Select(record => record.ItemId),
            baseInfoKey, donorInfoKey);
        WriteSupportSummary("AdditionalItemGrp", records.Count(record => record.AdditionalClassic),
            records.Count(record => !record.AdditionalClassic && record.AdditionalClassicAden),
            records.Where(record => !record.AdditionalClassic && !record.AdditionalClassicAden).Select(record => record.ItemId),
            baseAdditionalKey, donorAdditionalKey);
        WriteSupportSummary("ItemStatData", records.Count(record => record.StatsClassic),
            records.Count(record => !record.StatsClassic && record.StatsClassicAden),
            records.Where(record => !record.StatsClassic && !record.StatsClassicAden).Select(record => record.ItemId),
            baseStatsKey, donorStatsKey);

        if (args.Length == 4)
        {
            string outputPath = Path.GetFullPath(args[3]);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllText(outputPath, JsonSerializer.Serialize(records, _jsonOptions));
            Console.WriteLine($"Audit: {outputPath}");
        }

        return 0;
    }

    public static int VerifySpecialCraft(string[] args)
    {
        if (args.Length != 4)
            return Usage();

        PurchaseLimitCraftV7 data = ClientDatFile.Read<PurchaseLimitCraftV7>(args[1], out string rsaKey);
        NpcString npcStrings = ClientDatFile.Read<NpcString>(args[2], out _);
        IReadOnlyList<SpecialCraftServerProduct> serverProducts = SpecialCraftCatalog.ReadServerProducts(args[3]);
        SpecialCraftCatalog.Verify(data, npcStrings, serverProducts);
        Console.WriteLine($"Verified {serverProducts.Count} Special Craft records ({rsaKey}): {Path.GetFullPath(args[1])}");
        return 0;
    }

    public static int Usage()
    {
        Console.Error.WriteLine("Usage:");
        Console.Error.WriteLine("  L2Dn.ClientDat inspect <input.dat> [output.json]");
        Console.Error.WriteLine("  L2Dn.ClientDat audit-special-craft-client <craft.dat> <category.dat> <NpcString.dat> <L2GameDataName.dat> <ItemName.dat> <EtcItemGrp.dat> <ArmorGrp.dat> <WeaponGrp.dat> [output.json]");
        Console.Error.WriteLine("  L2Dn.ClientDat audit-special-craft-support <client-system-dir> <LimitShopCraft.xml> [output.json]");
        Console.Error.WriteLine("  L2Dn.ClientDat build-special-craft-bundle <client-system-dir> <LimitShopCraft.xml> <output-dir>");
        Console.Error.WriteLine("  L2Dn.ClientDat build-special-craft <base-classic.dat> <donor-classic-aden.dat> <npc-string-classic.dat> <LimitShopCraft.xml> <output.dat> <manifest.json>");
        Console.Error.WriteLine("  L2Dn.ClientDat verify-special-craft <input.dat> <npc-string-classic.dat> <LimitShopCraft.xml>");
        return 1;
    }

    private static string IconText(IndexedString value) => value.Text;

    private static void WriteSupportSummary(string table, int availableInClassic, int availableInDonor,
        IEnumerable<uint> unavailable, string classicKey, string donorKey)
    {
        uint[] missing = unavailable.ToArray();
        Console.WriteLine($"{table}: Classic={availableInClassic}, donor additions={availableInDonor}, " +
                          $"unavailable={missing.Length} ({classicKey}/{donorKey})");
        if (missing.Length != 0)
            Console.WriteLine($"  Missing: {string.Join(", ", missing)}");
    }

    private static string IdRange(IEnumerable<uint> values)
    {
        uint[] ids = values.ToArray();
        return ids.Length == 0 ? "empty" : $"{ids.Min()}..{ids.Max()}";
    }

    private sealed record AssetDescription(string Table, string[] Icons);

    private static void WriteManifest(string path, PurchaseLimitCraftV7 data)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, _jsonOptions));
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
