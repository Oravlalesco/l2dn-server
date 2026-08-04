using System.Xml.Linq;
using FluentAssertions;

namespace L2Dn.GameServer.StaticData.Tests;

public class DailyMissionCatalogTests
{
    private static readonly HashSet<string> KnownHandlers =
    [
        "level", "loginweekend", "loginmonth", "quest", "olympiad", "siege", "boss", "monster",
        "fishing", "spirit", "joinclan", "purge", "useitem",
    ];

    [Fact]
    public void Daily_mission_catalog_has_valid_protocol_ids_handlers_and_objectives()
    {
        XElement[] missions = XDocument.Load(DataPackPath("DailyMission.xml")).Root!.Elements("reward").ToArray();

        missions.Should().HaveCount(87);
        missions.Select(mission => (int)mission.Attribute("id")!).Should().OnlyHaveUniqueItems();
        missions.Should().OnlyContain(mission =>
            (int)mission.Attribute("id")! > 0 &&
            (int)mission.Attribute("id")! <= short.MaxValue &&
            (int)mission.Attribute("requiredCompletion")! > 0 &&
            mission.Element("items")!.Elements("item").Any() &&
            KnownHandlers.Contains((string)mission.Element("handler")!.Attribute("name")!));
    }

    [Fact]
    public void Monster_and_reward_ids_exist_and_are_not_duplicated_inside_a_mission()
    {
        HashSet<int> npcIds = LoadIds(DataPackPath("stats", "npcs"));
		Dictionary<int, bool> attackableNpcIds = Directory.EnumerateFiles(DataPackPath("stats", "npcs"), "*.xml")
			.SelectMany(path => XDocument.Load(path).Descendants("npc"))
			.GroupBy(npc => (int)npc.Attribute("id")!)
			.ToDictionary(group => group.Key, group => group.All(npc =>
				(string?)npc.Element("status")?.Attribute("attackable") != "false"));
        HashSet<int> itemIds = LoadIds(DataPackPath("stats", "items"));
        XElement[] missions = XDocument.Load(DataPackPath("DailyMission.xml")).Root!.Elements("reward").ToArray();

        foreach (XElement mission in missions)
        {
            int missionId = (int)mission.Attribute("id")!;
            int[] monsterIds = mission.Element("handler")!.Elements("param")
                .Where(parameter => (string)parameter.Attribute("name")! == "ids")
                .SelectMany(parameter => parameter.Value.Split(','))
                .Select(value => int.Parse(value.Trim()))
                .ToArray();

            monsterIds.Should().OnlyHaveUniqueItems($"mission {missionId} must not count the same monster twice");
            if (monsterIds.Length != 0)
            {
                monsterIds.Should().OnlyContain(id => npcIds.Contains(id),
                    $"mission {missionId} must reference existing NPCs");
				monsterIds.Should().OnlyContain(id => attackableNpcIds.GetValueOrDefault(id),
					$"mission {missionId} must reference attackable NPCs that can emit a kill");
            }

            int[] rewards = mission.Element("items")!.Elements("item")
                .Select(item => (int)item.Attribute("id")!)
                .Where(itemId => itemId is not 97224 and not 94481)
                .ToArray();
            rewards.Should().OnlyContain(id => itemIds.Contains(id),
                $"mission {missionId} must reward existing items");
            mission.Element("items")!.Elements("item")
                .Should().OnlyContain(item => (long)item.Attribute("count")! > 0);
        }
    }

    [Fact]
    public void Area_missions_have_attackable_spawn_origin_coverage()
    {
        XElement[] missions = XDocument.Load(DataPackPath("DailyMission.xml")).Root!.Elements("reward").ToArray();
        Dictionary<string, HashSet<int>> coverage = LoadDailyMissionAreaCoverage();

        foreach (XElement mission in missions.Where(mission =>
                     GetHandlerParameter(mission, "targetMode") is "SPAWN_AREAS" or "NPC_IDS_OR_SPAWN_AREAS"))
        {
            int missionId = (int)mission.Attribute("id")!;
            string[] areas = GetHandlerParameter(mission, "areas")!.Split(',', StringSplitOptions.TrimEntries);
            areas.Should().OnlyHaveUniqueItems($"mission {missionId} must not index the same area twice");
            areas.Where(area => !coverage.TryGetValue(area, out HashSet<int>? ids) || ids.Count == 0)
                .Should().BeEmpty($"every area of mission {missionId} must resolve to at least one spawn origin");
        }
    }

    [Fact]
    public void Regional_regressions_are_represented_by_origin_rules()
    {
        XElement[] missions = XDocument.Load(DataPackPath("DailyMission.xml")).Root!.Elements("reward").ToArray();
        Dictionary<string, HashSet<int>> coverage = LoadDailyMissionAreaCoverage();

        coverage["varka_silenos_barracks"].Should().Contain(21872,
            "new or formerly omitted Varka creatures must count by spawn origin");
        GetHandlerParameter(missions.Single(mission => (int)mission.Attribute("id")! == 1117), "excludedIds")
            .Should().Contain("20646", "the Giant's Cave entrance is explicitly excluded");
        GetHandlerParameter(missions.Single(mission => (int)mission.Attribute("id")! == 1121), "ids")
            .Should().Be("29105", "raid targets use the global attackable kill stream");
        GetHandlerParameter(missions.Single(mission => (int)mission.Attribute("id")! == 1103), "ids")
            .Should().NotContain("21658", "the non-attackable Forest of Mirrors placeholder cannot advance a mission");
    }

    [Fact]
    public void Generic_hunting_and_class_defaults_match_the_catalog_contract()
    {
        XElement[] missions = XDocument.Load(DataPackPath("DailyMission.xml")).Root!.Elements("reward").ToArray();
        XElement[] generic = missions.Where(mission =>
            (string)mission.Element("handler")!.Attribute("name")! == "monster" &&
            GetHandlerParameter(mission, "ids") == null && GetHandlerParameter(mission, "areas") == null).ToArray();

        generic.Should().NotBeEmpty();
        generic.Where(mission => GetHandlerParameter(mission, "minMonsterLevelOffset") is not null and not "-4")
            .Should().BeEmpty();
        missions.Should().OnlyContain(mission => (bool?)mission.Attribute("isMainClassOnly") != true,
            "missions are public to every active main, sub or dual class unless explicitly opted in");
        missions.Where(mission => (int)mission.Attribute("id")! is 1146 or 1147)
            .Should().OnlyContain(mission => (bool?)mission.Attribute("dailyReset") != false);
    }

    private static string? GetHandlerParameter(XElement mission, string name) => mission.Element("handler")!
        .Elements("param").SingleOrDefault(parameter => (string)parameter.Attribute("name")! == name)?.Value.Trim();

    private static Dictionary<string, HashSet<int>> LoadDailyMissionAreaCoverage()
    {
        Dictionary<string, HashSet<int>> result = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Directory.EnumerateFiles(DataPackPath("spawns"), "*.xml", SearchOption.AllDirectories))
        {
            XElement root = XDocument.Load(path).Root!;
            HashSet<string> listAreas = ResolveAreas([], root);
            foreach (XElement spawn in root.Elements("spawn"))
            {
                HashSet<string> spawnAreas = ResolveAreas(listAreas, spawn);
                foreach (XElement npc in spawn.Elements("npc"))
                {
                    AddCoverage(result, ResolveAreas(spawnAreas, npc), (int)npc.Attribute("id")!);
                }

                foreach (XElement group in spawn.Elements("group"))
                {
                    HashSet<string> groupAreas = ResolveAreas(spawnAreas, group);
                    foreach (XElement npc in group.Elements("npc"))
                    {
                        AddCoverage(result, ResolveAreas(groupAreas, npc), (int)npc.Attribute("id")!);
                    }
                }
            }
        }

        return result;
    }

    private static HashSet<string> ResolveAreas(IEnumerable<string> inherited, XElement element)
    {
        HashSet<string> result = inherited.ToHashSet(StringComparer.OrdinalIgnoreCase);
        result.UnionWith(SplitAreas((string?)element.Attribute("dailyMissionAreas")));
        result.ExceptWith(SplitAreas((string?)element.Attribute("excludedDailyMissionAreas")));
        return result;
    }

    private static IEnumerable<string> SplitAreas(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static void AddCoverage(Dictionary<string, HashSet<int>> coverage, IEnumerable<string> areas, int npcId)
    {
        foreach (string area in areas)
        {
            if (!coverage.TryGetValue(area, out HashSet<int>? ids))
            {
                coverage[area] = ids = [];
            }

            ids.Add(npcId);
        }
    }

    private static HashSet<int> LoadIds(string directory) => Directory
        .EnumerateFiles(directory, "*.xml")
        .SelectMany(path => XDocument.Load(path).Descendants())
        .Where(element => element.Name.LocalName is "npc" or "item" && element.Attribute("id") != null)
        .Select(element => (int)element.Attribute("id")!)
        .ToHashSet();

    private static string DataPackPath(params string[] segments)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string dataPack = Path.Combine(directory.FullName, "L2Dn.GameServer", "DataPack");
            if (Directory.Exists(dataPack))
            {
                return Path.Combine([dataPack, .. segments]);
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GameServer DataPack from the test output directory.");
    }
}
