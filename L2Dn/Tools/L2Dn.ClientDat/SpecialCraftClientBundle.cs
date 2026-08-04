using L2Dn.Packages.DatDefinitions;
using L2Dn.Packages.DatDefinitions.Definitions;

namespace L2Dn.ClientDat;

internal sealed record SpecialCraftClientVariantStats(
    int AddedItemNames,
    int AddedEtcItems,
    int AddedArmorItems,
    int AddedWeaponItems,
    int AddedBaseInfoItems,
    int AddedAdditionalItems,
    int AddedItemStats);

internal sealed record SpecialCraftClientBundleResult(
    PurchaseLimitCraftV7 ClassicCraft,
    PurchaseLimitCraftV7 ClassicAdenCraft,
    int ReferencedItems,
    SpecialCraftClientVariantStats Classic,
    SpecialCraftClientVariantStats ClassicAden,
    int AddedGameDataNames,
    int ReindexedClassicItemNames,
    int ReindexedClassicAdenItemNames);

internal static class SpecialCraftClientBundle
{
    private const string EuDirectory = "eu";

    public static SpecialCraftClientBundleResult Build(string clientSystemPath, string catalogPath,
        string outputDirectory)
    {
        string clientSystem = Path.GetFullPath(clientSystemPath);
        string clientEu = Path.Combine(clientSystem, EuDirectory);
        string output = Path.GetFullPath(outputDirectory);

        string nameDataPath = RequiredFile(clientEu, "L2GameDataName.dat");
        L2NameData nameData = ClientDatFile.Read<L2NameData>(nameDataPath, out _);
        DatReader.SetNameData(nameData.Names);

        PurchaseLimitCraftV7 baseCraft = Read<PurchaseLimitCraftV7>(clientEu,
            "PurchaseLimitCraft_Classic-eu.dat");
        PurchaseLimitCraftV7 donorCraft = Read<PurchaseLimitCraftV7>(clientEu,
            "PurchaseLimitCraft_ClassicAden-eu.dat");
        NpcString npcStrings = Read<NpcString>(clientEu, "NpcString_Classic-eu.dat");
        ItemNameV18 baseNames = Read<ItemNameV18>(clientEu, "ItemName_Classic-eu.dat");
        ItemNameV18 donorNames = Read<ItemNameV18>(clientEu, "ItemName_ClassicAden-eu.dat");
        EtcItemGrpV9 baseEtc = Read<EtcItemGrpV9>(clientEu, "EtcItemgrp_Classic.dat");
        EtcItemGrpV9 donorEtc = Read<EtcItemGrpV9>(clientEu, "EtcItemgrp_ClassicAden.dat");
        ArmorGrpV14 baseArmor = Read<ArmorGrpV14>(clientEu, "Armorgrp_Classic.dat");
        ArmorGrpV14 donorArmor = Read<ArmorGrpV14>(clientEu, "Armorgrp_ClassicAden.dat");
        WeaponGrpV12 baseWeapon = Read<WeaponGrpV12>(clientEu, "Weapongrp_Classic.dat");
        WeaponGrpV12 donorWeapon = Read<WeaponGrpV12>(clientEu, "Weapongrp_ClassicAden.dat");
        ItemBaseInfoV5 baseInfo = Read<ItemBaseInfoV5>(clientEu, "item_baseinfo_Classic.dat");
        ItemBaseInfoV5 donorInfo = Read<ItemBaseInfoV5>(clientEu, "item_baseinfo_ClassicAden.dat");
        AdditionalItemGrpV4 baseAdditional = Read<AdditionalItemGrpV4>(clientEu,
            "AdditionalItemGrp_Classic.dat");
        AdditionalItemGrpV4 donorAdditional = Read<AdditionalItemGrpV4>(clientEu,
            "AdditionalItemGrp_ClassicAden.dat");
        ItemStatDataV4 baseStats = Read<ItemStatDataV4>(clientEu, "ItemStatData_Classic.dat");
        ItemStatDataV4 donorStats = Read<ItemStatDataV4>(clientEu, "ItemStatData_ClassicAden.dat");

        IReadOnlyList<SpecialCraftServerProduct> serverProducts =
            SpecialCraftCatalog.ReadServerProducts(catalogPath);
        IReadOnlyDictionary<uint, string> serverItemNames =
            SpecialCraftCatalog.ReadServerItemNames(catalogPath);
        PurchaseLimitCraftV7 classicCraft = SpecialCraftCatalog.Build(baseCraft, donorCraft, npcStrings,
            serverProducts);
        PurchaseLimitCraftV7 classicAdenCraft = SpecialCraftCatalog.Build(donorCraft, baseCraft, npcStrings,
            serverProducts);
        HashSet<uint> referencedItemIds = serverProducts
            .SelectMany(product => product.Ingredients.Select(ingredient => ingredient.ItemId)
                .Concat(product.Outcomes.Select(outcome => outcome.ItemId)))
            .ToHashSet();

        ItemNameV18 classicNames = MergeItemNames(baseNames, donorNames, referencedItemIds,
            out int addedClassicNames);
        ItemNameV18 classicAdenNames = MergeItemNames(donorNames, baseNames, referencedItemIds,
            out int addedClassicAdenNames);
        L2NameData normalizedNameData = NormalizeItemNames(nameData, classicNames, serverItemNames,
            referencedItemIds, out int firstAddedGameDataNames, out int reindexedClassicNames);
        normalizedNameData = NormalizeItemNames(normalizedNameData, classicAdenNames, serverItemNames,
            referencedItemIds, out int secondAddedGameDataNames, out int reindexedClassicAdenNames);

        (EtcItemGrpV9 classicEtc, ArmorGrpV14 classicArmor, WeaponGrpV12 classicWeapon,
            int addedClassicEtc, int addedClassicArmor, int addedClassicWeapon) = MergeVisualAssets(
            baseEtc, donorEtc, baseArmor, donorArmor, baseWeapon, donorWeapon, referencedItemIds);
        (EtcItemGrpV9 classicAdenEtc, ArmorGrpV14 classicAdenArmor, WeaponGrpV12 classicAdenWeapon,
            int addedClassicAdenEtc, int addedClassicAdenArmor, int addedClassicAdenWeapon) = MergeVisualAssets(
            donorEtc, baseEtc, donorArmor, baseArmor, donorWeapon, baseWeapon, referencedItemIds);

        ItemBaseInfoV5 classicInfo = new()
        {
            Records = MergeRequiredRecords(baseInfo.Records, donorInfo.Records, referencedItemIds,
                record => record.ItemId, "item_baseinfo Classic", out int addedClassicBaseInfo),
        };
        ItemBaseInfoV5 classicAdenInfo = new()
        {
            Records = MergeRequiredRecords(donorInfo.Records, baseInfo.Records, referencedItemIds,
                record => record.ItemId, "item_baseinfo ClassicAden", out int addedClassicAdenBaseInfo),
        };
        AdditionalItemGrpV4 classicAdditional = new()
        {
            Records = MergeRequiredRecords(baseAdditional.Records, donorAdditional.Records, referencedItemIds,
                record => record.Id, "AdditionalItemGrp Classic", out int addedClassicAdditional),
        };
        AdditionalItemGrpV4 classicAdenAdditional = new()
        {
            Records = MergeRequiredRecords(donorAdditional.Records, baseAdditional.Records, referencedItemIds,
                record => record.Id, "AdditionalItemGrp ClassicAden", out int addedClassicAdenAdditional),
        };
        ItemStatDataV4 classicStats = new()
        {
            Records = MergeRequiredRecords(baseStats.Records, donorStats.Records, referencedItemIds,
                record => record.ItemId, "ItemStatData Classic", out int addedClassicStats),
        };
        ItemStatDataV4 classicAdenStats = new()
        {
            Records = MergeRequiredRecords(donorStats.Records, baseStats.Records, referencedItemIds,
                record => record.ItemId, "ItemStatData ClassicAden", out int addedClassicAdenStats),
        };

        Directory.CreateDirectory(output);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, "L2GameDataName.dat"), normalizedNameData);
        WriteVariant(output, "Classic", classicCraft, classicNames, classicEtc, classicArmor, classicWeapon,
            classicInfo, classicAdditional, classicStats);
        WriteVariant(output, "ClassicAden", classicAdenCraft, classicAdenNames, classicAdenEtc,
            classicAdenArmor, classicAdenWeapon, classicAdenInfo, classicAdenAdditional, classicAdenStats);

        Verify(output, npcStrings, serverProducts, serverItemNames, referencedItemIds);
        SpecialCraftClientVariantStats classicVariantStats = new(addedClassicNames, addedClassicEtc,
            addedClassicArmor, addedClassicWeapon, addedClassicBaseInfo, addedClassicAdditional,
            addedClassicStats);
        SpecialCraftClientVariantStats classicAdenVariantStats = new(addedClassicAdenNames,
            addedClassicAdenEtc, addedClassicAdenArmor, addedClassicAdenWeapon, addedClassicAdenBaseInfo,
            addedClassicAdenAdditional, addedClassicAdenStats);
        return new SpecialCraftClientBundleResult(classicCraft, classicAdenCraft, referencedItemIds.Count,
            classicVariantStats, classicAdenVariantStats,
            firstAddedGameDataNames + secondAddedGameDataNames, reindexedClassicNames,
            reindexedClassicAdenNames);
    }

    private static void WriteVariant(string output, string variant, PurchaseLimitCraftV7 craft,
        ItemNameV18 names, EtcItemGrpV9 etc, ArmorGrpV14 armor, WeaponGrpV12 weapon,
        ItemBaseInfoV5 info, AdditionalItemGrpV4 additional, ItemStatDataV4 stats)
    {
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, $"PurchaseLimitCraft_{variant}-eu.dat"), craft);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, $"ItemName_{variant}-eu.dat"), names);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, $"EtcItemgrp_{variant}.dat"), etc);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, $"Armorgrp_{variant}.dat"), armor);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, $"Weapongrp_{variant}.dat"), weapon);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, $"item_baseinfo_{variant}.dat"), info);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, $"AdditionalItemGrp_{variant}.dat"), additional);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, $"ItemStatData_{variant}.dat"), stats);
    }

    private static ItemNameV18 MergeItemNames(ItemNameV18 baseData, ItemNameV18 donorData,
        IReadOnlySet<uint> requiredIds, out int additions)
    {
        Dictionary<uint, ItemNameV18.ItemNameRecord> baseById = baseData.Records
            .GroupBy(record => record.Id)
            .ToDictionary(group => group.Key, group => group.First());
        Dictionary<uint, ItemNameV18.ItemNameRecord> donorById = donorData.Records
            .GroupBy(record => record.Id)
            .ToDictionary(group => group.Key, group => group.First());
        HashSet<uint> replacements = new();
        List<ItemNameV18.ItemNameRecord> donorRecords = new();
        List<uint> missing = new();

        foreach (uint itemId in requiredIds.Order())
        {
            if (baseById.TryGetValue(itemId, out ItemNameV18.ItemNameRecord? baseRecord) &&
                !string.IsNullOrWhiteSpace(baseRecord.Name.Text))
            {
                continue;
            }

            if (donorById.TryGetValue(itemId, out ItemNameV18.ItemNameRecord? donorRecord) &&
                !string.IsNullOrWhiteSpace(donorRecord.Name.Text))
            {
                replacements.Add(itemId);
                donorRecords.Add(donorRecord);
            }
            else
            {
                missing.Add(itemId);
            }
        }

        if (missing.Count != 0)
            throw new InvalidDataException($"Items without a Classic/Classic Aden client name: {string.Join(", ", missing)}");

        additions = donorRecords.Count;
        HashSet<uint> baseMacroIds = baseData.Macros.Select(macro => macro.MacroId).ToHashSet();
        HashSet<uint> baseItemExIds = baseData.ItemExs.Select(item => item.ItemExId).ToHashSet();
        return new ItemNameV18
        {
            Records = baseData.Records.Where(record => !replacements.Contains(record.Id))
                .Concat(donorRecords)
                .ToArray(),
            Macros = baseData.Macros.Concat(donorData.Macros.Where(macro =>
                    requiredIds.Contains(macro.MacroId) && !baseMacroIds.Contains(macro.MacroId)))
                .ToArray(),
            ItemExs = baseData.ItemExs.Concat(donorData.ItemExs.Where(item =>
                    requiredIds.Contains(item.ItemExId) && !baseItemExIds.Contains(item.ItemExId)))
                .ToArray(),
        };
    }

    private static L2NameData NormalizeItemNames(L2NameData nameData, ItemNameV18 itemNames,
        IReadOnlyDictionary<uint, string> serverItemNames, IReadOnlySet<uint> requiredIds,
        out int addedGameDataNames, out int reindexedItemNames)
    {
        List<string> names = nameData.Names.ToList();
        Dictionary<string, int> indexByName = names
            .Select((name, index) => (name, index))
            .GroupBy(entry => entry.name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().index, StringComparer.Ordinal);
        Dictionary<uint, ItemNameV18.ItemNameRecord> recordsById = ToDictionary(itemNames.Records,
            record => record.Id);
        reindexedItemNames = 0;

        foreach (uint itemId in requiredIds.Order())
        {
            if (!recordsById.TryGetValue(itemId, out ItemNameV18.ItemNameRecord? record))
                throw new InvalidDataException($"ItemName has no record for required item {itemId}.");
            if (!serverItemNames.TryGetValue(itemId, out string? desiredName) ||
                string.IsNullOrWhiteSpace(desiredName))
            {
                throw new InvalidDataException($"The server has no name for required item {itemId}.");
            }

            if (string.Equals(record.Name.Text, desiredName, StringComparison.Ordinal))
                continue;

            if (!indexByName.TryGetValue(desiredName, out int index))
            {
                index = names.Count;
                names.Add(desiredName);
                indexByName.Add(desiredName, index);
            }

            record.Name = new IndexedString(desiredName, index);
            reindexedItemNames++;
        }

        addedGameDataNames = names.Count - nameData.Names.Length;
        return new L2NameData { Names = names.ToArray() };
    }

    private static (EtcItemGrpV9 Etc, ArmorGrpV14 Armor, WeaponGrpV12 Weapon,
        int AddedEtc, int AddedArmor, int AddedWeapon) MergeVisualAssets(
        EtcItemGrpV9 baseEtc, EtcItemGrpV9 donorEtc,
        ArmorGrpV14 baseArmor, ArmorGrpV14 donorArmor,
        WeaponGrpV12 baseWeapon, WeaponGrpV12 donorWeapon,
        IReadOnlySet<uint> requiredIds)
    {
        Dictionary<uint, EtcItemGrpV9.EtcItemGrpRecord> baseEtcById = ToDictionary(baseEtc.Records,
            record => record.ObjectId);
        Dictionary<uint, ArmorGrpV14.ArmorGrpRecord> baseArmorById = ToDictionary(baseArmor.Records,
            record => record.ObjectId);
        Dictionary<uint, WeaponGrpV12.WeaponGrpRecord> baseWeaponById = ToDictionary(baseWeapon.Records,
            record => record.ObjectId);
        Dictionary<uint, EtcItemGrpV9.EtcItemGrpRecord> donorEtcById = ToDictionary(donorEtc.Records,
            record => record.ObjectId);
        Dictionary<uint, ArmorGrpV14.ArmorGrpRecord> donorArmorById = ToDictionary(donorArmor.Records,
            record => record.ObjectId);
        Dictionary<uint, WeaponGrpV12.WeaponGrpRecord> donorWeaponById = ToDictionary(donorWeapon.Records,
            record => record.ObjectId);

        List<EtcItemGrpV9.EtcItemGrpRecord> etcAdditions = new();
        List<ArmorGrpV14.ArmorGrpRecord> armorAdditions = new();
        List<WeaponGrpV12.WeaponGrpRecord> weaponAdditions = new();
        HashSet<uint> replacements = new();
        List<uint> missing = new();

        foreach (uint itemId in requiredIds.Order())
        {
            if (HasValidVisual(itemId, baseEtcById, baseArmorById, baseWeaponById))
                continue;

            replacements.Add(itemId);
            if (donorEtcById.TryGetValue(itemId, out EtcItemGrpV9.EtcItemGrpRecord? etcRecord) &&
                HasIcon(etcRecord.Icons))
            {
                etcAdditions.Add(etcRecord);
            }
            else if (donorArmorById.TryGetValue(itemId, out ArmorGrpV14.ArmorGrpRecord? armorRecord) &&
                     HasIcon(armorRecord.Icons))
            {
                armorAdditions.Add(armorRecord);
            }
            else if (donorWeaponById.TryGetValue(itemId, out WeaponGrpV12.WeaponGrpRecord? weaponRecord) &&
                     HasIcon(weaponRecord.Icons))
            {
                weaponAdditions.Add(weaponRecord);
            }
            else
            {
                missing.Add(itemId);
            }
        }

        if (missing.Count != 0)
            throw new InvalidDataException($"Items without a Classic/Classic Aden visual asset: {string.Join(", ", missing)}");

        EtcItemGrpV9 etcResult = new()
        {
            Records = baseEtc.Records.Where(record => !replacements.Contains(record.ObjectId))
                .Concat(etcAdditions)
                .ToArray(),
        };
        ArmorGrpV14 armorResult = new()
        {
            Records = baseArmor.Records.Where(record => !replacements.Contains(record.ObjectId))
                .Concat(armorAdditions)
                .ToArray(),
        };
        WeaponGrpV12 weaponResult = new()
        {
            Records = baseWeapon.Records.Where(record => !replacements.Contains(record.ObjectId))
                .Concat(weaponAdditions)
                .ToArray(),
        };
        return (etcResult, armorResult, weaponResult, etcAdditions.Count, armorAdditions.Count,
            weaponAdditions.Count);
    }

    private static T[] MergeRequiredRecords<T>(IEnumerable<T> baseRecords, IEnumerable<T> donorRecords,
        IReadOnlySet<uint> requiredIds, Func<T, uint> idSelector, string tableName, out int additions)
    {
        T[] baseArray = baseRecords.ToArray();
        HashSet<uint> baseIds = baseArray.Select(idSelector).ToHashSet();
        Dictionary<uint, T> donorById = donorRecords
            .GroupBy(idSelector)
            .ToDictionary(group => group.Key, group => group.First());
        List<T> donorAdditions = new();
        List<uint> missing = new();

        foreach (uint itemId in requiredIds.Order())
        {
            if (baseIds.Contains(itemId))
                continue;

            if (donorById.TryGetValue(itemId, out T? donorRecord))
                donorAdditions.Add(donorRecord);
            else
                missing.Add(itemId);
        }

        if (missing.Count != 0)
            throw new InvalidDataException($"Items absent from Classic/Classic Aden {tableName}: {string.Join(", ", missing)}");

        additions = donorAdditions.Count;
        return baseArray.Concat(donorAdditions).ToArray();
    }

    private static void Verify(string outputDirectory, NpcString npcStrings,
        IReadOnlyList<SpecialCraftServerProduct> serverProducts,
        IReadOnlyDictionary<uint, string> serverItemNames, IReadOnlySet<uint> requiredIds)
    {
        L2NameData nameData = Read<L2NameData>(outputDirectory, "L2GameDataName.dat");
        DatReader.SetNameData(nameData.Names);
        VerifyVariant(outputDirectory, "Classic", npcStrings, serverProducts, serverItemNames, requiredIds);
        VerifyVariant(outputDirectory, "ClassicAden", npcStrings, serverProducts, serverItemNames, requiredIds);
    }

    private static void VerifyVariant(string outputDirectory, string variant, NpcString npcStrings,
        IReadOnlyList<SpecialCraftServerProduct> serverProducts,
        IReadOnlyDictionary<uint, string> serverItemNames, IReadOnlySet<uint> requiredIds)
    {
        PurchaseLimitCraftV7 craft = Read<PurchaseLimitCraftV7>(outputDirectory,
            $"PurchaseLimitCraft_{variant}-eu.dat");
        SpecialCraftCatalog.Verify(craft, npcStrings, serverProducts);

        ItemNameV18 names = Read<ItemNameV18>(outputDirectory, $"ItemName_{variant}-eu.dat");
        EtcItemGrpV9 etc = Read<EtcItemGrpV9>(outputDirectory, $"EtcItemgrp_{variant}.dat");
        ArmorGrpV14 armor = Read<ArmorGrpV14>(outputDirectory, $"Armorgrp_{variant}.dat");
        WeaponGrpV12 weapon = Read<WeaponGrpV12>(outputDirectory, $"Weapongrp_{variant}.dat");
        ItemBaseInfoV5 itemInfo = Read<ItemBaseInfoV5>(outputDirectory, $"item_baseinfo_{variant}.dat");
        AdditionalItemGrpV4 additionalItems = Read<AdditionalItemGrpV4>(outputDirectory,
            $"AdditionalItemGrp_{variant}.dat");
        ItemStatDataV4 itemStats = Read<ItemStatDataV4>(outputDirectory, $"ItemStatData_{variant}.dat");
        Dictionary<uint, ItemNameV18.ItemNameRecord> namesById = ToDictionary(names.Records, record => record.Id);
        Dictionary<uint, EtcItemGrpV9.EtcItemGrpRecord> etcById = ToDictionary(etc.Records, record => record.ObjectId);
        Dictionary<uint, ArmorGrpV14.ArmorGrpRecord> armorById = ToDictionary(armor.Records,
            record => record.ObjectId);
        Dictionary<uint, WeaponGrpV12.WeaponGrpRecord> weaponById = ToDictionary(weapon.Records,
            record => record.ObjectId);

        uint[] missingNames = requiredIds.Where(itemId =>
                !namesById.TryGetValue(itemId, out ItemNameV18.ItemNameRecord? record) ||
                string.IsNullOrWhiteSpace(record.Name.Text))
            .Order()
            .ToArray();
        if (missingNames.Length != 0)
            throw new InvalidDataException($"Generated {variant} bundle has items without names: {string.Join(", ", missingNames)}");

        uint[] mismatchedNames = requiredIds.Where(itemId =>
                !namesById.TryGetValue(itemId, out ItemNameV18.ItemNameRecord? record) ||
                !serverItemNames.TryGetValue(itemId, out string? serverName) ||
                !string.Equals(record.Name.Text, serverName, StringComparison.Ordinal))
            .Order()
            .ToArray();
        if (mismatchedNames.Length != 0)
            throw new InvalidDataException($"Generated {variant} bundle has client/server name mismatches: {string.Join(", ", mismatchedNames)}");

        uint[] missingVisuals = requiredIds.Where(itemId =>
                !HasValidVisual(itemId, etcById, armorById, weaponById))
            .Order()
            .ToArray();
        if (missingVisuals.Length != 0)
            throw new InvalidDataException($"Generated {variant} bundle has items without visual assets: {string.Join(", ", missingVisuals)}");

        VerifyRequiredIds($"item_baseinfo_{variant}", requiredIds,
            itemInfo.Records.Select(record => record.ItemId));
        VerifyRequiredIds($"AdditionalItemGrp_{variant}", requiredIds,
            additionalItems.Records.Select(record => record.Id));
        VerifyRequiredIds($"ItemStatData_{variant}", requiredIds,
            itemStats.Records.Select(record => record.ItemId));
    }

    private static void VerifyRequiredIds(string tableName, IEnumerable<uint> requiredIds,
        IEnumerable<uint> availableIds)
    {
        HashSet<uint> available = availableIds.ToHashSet();
        uint[] missing = requiredIds.Where(itemId => !available.Contains(itemId)).Order().ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException($"Generated bundle has items absent from {tableName}: {string.Join(", ", missing)}");
    }

    private static bool HasValidVisual(uint itemId,
        IReadOnlyDictionary<uint, EtcItemGrpV9.EtcItemGrpRecord> etc,
        IReadOnlyDictionary<uint, ArmorGrpV14.ArmorGrpRecord> armor,
        IReadOnlyDictionary<uint, WeaponGrpV12.WeaponGrpRecord> weapon) =>
        (etc.TryGetValue(itemId, out EtcItemGrpV9.EtcItemGrpRecord? etcRecord) && HasIcon(etcRecord.Icons)) ||
        (armor.TryGetValue(itemId, out ArmorGrpV14.ArmorGrpRecord? armorRecord) && HasIcon(armorRecord.Icons)) ||
        (weapon.TryGetValue(itemId, out WeaponGrpV12.WeaponGrpRecord? weaponRecord) && HasIcon(weaponRecord.Icons));

    private static bool HasIcon(IEnumerable<IndexedString> icons) =>
        icons.Any(icon => !string.IsNullOrWhiteSpace(icon.Text));

    private static Dictionary<uint, T> ToDictionary<T>(IEnumerable<T> records, Func<T, uint> idSelector) =>
        records.GroupBy(idSelector).ToDictionary(group => group.Key, group => group.First());

    private static T Read<T>(string directory, string fileName) =>
        ClientDatFile.Read<T>(RequiredFile(directory, fileName), out _);

    private static string RequiredFile(string directory, string fileName)
    {
        string path = Path.Combine(directory, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Required client file not found: {path}", path);
        return path;
    }
}
