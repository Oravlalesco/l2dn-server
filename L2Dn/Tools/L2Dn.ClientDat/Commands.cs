using System.Text.Json;
using System.Text.Json.Serialization;
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
        Console.Error.WriteLine("  L2Dn.ClientDat build-special-craft <base-classic.dat> <donor-classic-aden.dat> <npc-string-classic.dat> <LimitShopCraft.xml> <output.dat> <manifest.json>");
        Console.Error.WriteLine("  L2Dn.ClientDat verify-special-craft <input.dat> <npc-string-classic.dat> <LimitShopCraft.xml>");
        return 1;
    }

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
