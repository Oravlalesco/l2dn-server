using L2Dn.Packages.DatDefinitions;
using L2Dn.Packages.DatDefinitions.Definitions;

namespace L2Dn.ClientDat;

internal sealed record SpecialCraftClientBundleResult(
    PurchaseLimitCraftV7 Craft,
    int ReferencedItems,
    int AddedItemNames,
    int AddedEtcItems,
    int AddedArmorItems,
    int AddedWeaponItems);

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

        IReadOnlyList<SpecialCraftServerProduct> serverProducts =
            SpecialCraftCatalog.ReadServerProducts(catalogPath);
        PurchaseLimitCraftV7 craft = SpecialCraftCatalog.Build(baseCraft, donorCraft, npcStrings, serverProducts);
        HashSet<uint> referencedItemIds = serverProducts
            .SelectMany(product => product.Ingredients.Select(ingredient => ingredient.ItemId)
                .Concat(product.Outcomes.Select(outcome => outcome.ItemId)))
            .ToHashSet();

        ItemNameV18 itemNames = MergeItemNames(baseNames, donorNames, referencedItemIds, out int addedNames);
        (EtcItemGrpV9 etcItems, ArmorGrpV14 armorItems, WeaponGrpV12 weaponItems,
            int addedEtc, int addedArmor, int addedWeapon) = MergeVisualAssets(
            baseEtc, donorEtc, baseArmor, donorArmor, baseWeapon, donorWeapon, referencedItemIds);

        Directory.CreateDirectory(output);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, "PurchaseLimitCraft_Classic-eu.dat"), craft);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, "ItemName_Classic-eu.dat"), itemNames);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, "EtcItemgrp_Classic.dat"), etcItems);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, "Armorgrp_Classic.dat"), armorItems);
        ClientDatFile.WriteLineage2Ver413(Path.Combine(output, "Weapongrp_Classic.dat"), weaponItems);

        Verify(output, npcStrings, serverProducts, referencedItemIds);
        return new SpecialCraftClientBundleResult(craft, referencedItemIds.Count, addedNames, addedEtc,
            addedArmor, addedWeapon);
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

    private static void Verify(string outputDirectory, NpcString npcStrings,
        IReadOnlyList<SpecialCraftServerProduct> serverProducts, IReadOnlySet<uint> requiredIds)
    {
        PurchaseLimitCraftV7 craft = Read<PurchaseLimitCraftV7>(outputDirectory,
            "PurchaseLimitCraft_Classic-eu.dat");
        SpecialCraftCatalog.Verify(craft, npcStrings, serverProducts);

        ItemNameV18 names = Read<ItemNameV18>(outputDirectory, "ItemName_Classic-eu.dat");
        EtcItemGrpV9 etc = Read<EtcItemGrpV9>(outputDirectory, "EtcItemgrp_Classic.dat");
        ArmorGrpV14 armor = Read<ArmorGrpV14>(outputDirectory, "Armorgrp_Classic.dat");
        WeaponGrpV12 weapon = Read<WeaponGrpV12>(outputDirectory, "Weapongrp_Classic.dat");
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
            throw new InvalidDataException($"Generated bundle has items without names: {string.Join(", ", missingNames)}");

        uint[] missingVisuals = requiredIds.Where(itemId =>
                !HasValidVisual(itemId, etcById, armorById, weaponById))
            .Order()
            .ToArray();
        if (missingVisuals.Length != 0)
            throw new InvalidDataException($"Generated bundle has items without visual assets: {string.Join(", ", missingVisuals)}");
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
