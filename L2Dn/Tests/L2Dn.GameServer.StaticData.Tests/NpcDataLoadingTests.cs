using FluentAssertions;
using L2Dn.GameServer.Configuration;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.Skills;
using System.Xml.Linq;

namespace L2Dn.GameServer.StaticData.Tests;

public sealed class NpcDataLoadingTests
{
    [Fact]
    public void LoadsSkillsFromSchemaCompliantSkillListElements()
    {
        (string dataPackPath, string configPath) = LocateGameServerData();
        ServerConfig.Instance.DataPack.Path = dataPackPath;
        ServerConfig.Instance.DataPack.ConfigPath = configPath;
        Scripts.Scripts.RegisterHandlers();

        NpcData npcData = NpcData.getInstance();
        NpcTemplate? gremlin = npcData.getTemplate(20001);
        NpcTemplate? rangedLaboratoryNpc = npcData.getTemplate(21101);
        NpcTemplate? survivalLaboratoryNpc = npcData.getTemplate(20292);
        NpcTemplate? crasher = npcData.getTemplate(20101);
        NpcTemplate? undineNoble = npcData.getTemplate(20115);
        NpcTemplate? aggressiveLaboratoryNpc = npcData.getTemplate(20130);

        gremlin.Should().NotBeNull();
        gremlin!.getSkills().Should().ContainKey(4408);

        rangedLaboratoryNpc.Should().NotBeNull();
        rangedLaboratoryNpc!.getSkills().Should().ContainKeys(4152, 4160);
        rangedLaboratoryNpc.getAISkills(AISkillScope.ATTACK)
            .Should().Contain(skill => skill.getId() == 4152);

        survivalLaboratoryNpc.Should().NotBeNull();
        survivalLaboratoryNpc!.getSkills().Should().ContainKeys(4065, 4151, 4160);
        survivalLaboratoryNpc.getAISkills(AISkillScope.HEAL)
            .Should().Contain(skill => skill.getId() == 4065);
        survivalLaboratoryNpc.getAISkills(AISkillScope.ATTACK)
            .Should().Contain(skill => skill.getId() == 4151 || skill.getId() == 4160);

        crasher.Should().NotBeNull();
        crasher!.getAIType().Should().Be(AIType.FIGHTER);
        crasher.getAISkills(AISkillScope.ATTACK).Should().Contain(skill => skill.getId() == 4247);

        undineNoble.Should().NotBeNull();
        undineNoble!.getAIType().Should().Be(AIType.FIGHTER);
        undineNoble.getAISkills(AISkillScope.ATTACK).Should().Contain(skill => skill.getId() == 4001);

        aggressiveLaboratoryNpc.Should().NotBeNull();
        Skill aggressiveStun = aggressiveLaboratoryNpc!.getAISkills(AISkillScope.DEBUFF)
            .Single(skill => skill.getId() == 4072);
        aggressiveStun.isMagic().Should().BeFalse();
        aggressiveStun.getCastRange().Should().BeLessThanOrEqualTo(0);
    }

    [Fact]
    public void Talking_island_rollout_covers_all_spawned_templates_and_only_base_monsters()
    {
        int[] expectedTemplateIds =
        [
            20006, 20016, 20093, 20096, 20098, 20101, 20103, 20106, 20108, 20110, 20113, 20115,
            20120, 20121, 20130, 20131, 20132, 20326, 20342, 20343, 20432, 20442, 20481, 20544
        ];
        int[] rangedControlIds = [20006, 20101, 20110, 20113, 20115];
        (string dataPackPath, string configPath) = LocateGameServerData();
        ServerConfig.Instance.DataPack.Path = dataPackPath;
        ServerConfig.Instance.DataPack.ConfigPath = configPath;
        Scripts.Scripts.RegisterHandlers();

        string spawnDirectory = Path.Combine(dataPackPath, "spawns", "TalkingIsland");
        int[] spawnedTemplateIds = Directory.GetFiles(spawnDirectory, "*.xml", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).Equals("StrategyValidationLab.xml", StringComparison.OrdinalIgnoreCase))
            .Select(XDocument.Load)
            .SelectMany(document => document.Descendants("npc"))
            .Select(element => (int)element.Attribute("id")!)
            .Distinct()
            .Order()
            .ToArray();
        NpcData npcData = NpcData.getInstance();
        NpcTemplate[] templates = spawnedTemplateIds.Select(id => npcData.getTemplate(id))
            .Where(template => template != null)
            .Cast<NpcTemplate>()
            .ToArray();

        spawnedTemplateIds.Should().Equal(expectedTemplateIds);
        templates.Should().HaveCount(expectedTemplateIds.Length);
        templates.Should().OnlyContain(template => template.getType() == "Monster");
        templates.Where(template => rangedControlIds.Contains(template.getId()) &&
                template.getAIType() != AIType.ARCHER &&
                template.getAISkills(AISkillScope.LONG_RANGE).Count == 0)
            .Select(template => template.getId())
            .Should().BeEmpty("every RangedControl assignment needs Archer or long-range AI capability");
        templates.Single(template => template.getId() == 20006).getBaseAttackRange()
            .Should().BeGreaterThanOrEqualTo(500, "Orc Archer must shoot from bow range, not melee");
        templates.SelectMany(template => template.getAISkills(AISkillScope.HEAL))
            .Should().BeEmpty("Talking Island has no heal-capable base mob for the Survival profile");
    }

    [Fact]
    public void Archer_templates_use_ranged_physical_attack_range()
    {
        (string dataPackPath, string configPath) = LocateGameServerData();
        ServerConfig.Instance.DataPack.Path = dataPackPath;
        ServerConfig.Instance.DataPack.ConfigPath = configPath;
        Scripts.Scripts.RegisterHandlers();

        NpcData.getInstance().getTemplates(template => template.getAIType() == AIType.ARCHER)
            .Where(template => template.getBaseAttackRange() < 500)
            .Select(template => template.getId())
            .Should().BeEmpty("ARCHER templates must not inherit melee attack range");
    }

    [Fact]
    public void Strategy_validation_lab_stages_three_base_monsters_per_profile()
    {
        Dictionary<string, int[]> expectedGroups = new()
        {
            ["Strategy Lab - Balanced"] = [20016, 20016, 20016],
            ["Strategy Lab - Aggressive Pressure"] = [20130, 20130, 20130],
            ["Strategy Lab - Ranged Control"] = [20115, 20115, 20115],
            ["Strategy Lab - Survival"] = [20292, 20292, 20292],
            ["Strategy Lab - Aggressive Wave Captain"] = [20098, 20098],
            ["Strategy Lab - Aggressive Wave Spiders"] = [20103, 20106, 20108],
            ["Strategy Lab - Aggressive Wave Melee"] = [20132, 20326, 20131],
            ["Strategy Lab - Ranged Wave Undines"] = [20110, 20113],
            ["Strategy Lab - Ranged Wave Crasher"] = [20101, 20101],
            ["Strategy Lab - Ranged Wave Archer"] = [20006, 20006],
        };
        (string dataPackPath, string configPath) = LocateGameServerData();
        ServerConfig.Instance.DataPack.Path = dataPackPath;
        ServerConfig.Instance.DataPack.ConfigPath = configPath;
        Scripts.Scripts.RegisterHandlers();

        string labPath = Path.Combine(dataPackPath, "spawns", "TalkingIsland", "StrategyValidationLab.xml");
        XDocument document = XDocument.Load(labPath);
        XElement[] groups = document.Descendants("group").ToArray();
        XElement[] npcs = groups.SelectMany(group => group.Elements("npc")).ToArray();

        groups.Should().HaveCount(expectedGroups.Count);
        foreach (XElement group in groups)
        {
            string name = (string)group.Attribute("name")!;
            expectedGroups.Should().ContainKey(name);
            group.Elements("npc").Select(npc => (int)npc.Attribute("id")!)
                .Should().Equal(expectedGroups[name]);
        }

        npcs.Should().HaveCount(26);
        npcs.Select(npc => ((int)npc.Attribute("x")!, (int)npc.Attribute("y")!))
            .Should().OnlyHaveUniqueItems("laboratory mobs must not overlap at their declared anchors");
        npcs.Should().OnlyContain(npc => (string)npc.Attribute("respawnTime")! == "20sec");

        NpcData npcData = NpcData.getInstance();
        expectedGroups.Values.SelectMany(ids => ids).Distinct().Select(id => npcData.getTemplate(id))
            .Should().OnlyContain(template => template != null && template.getType() == "Monster");
    }

    private static (string DataPackPath, string ConfigPath) LocateGameServerData()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string gameServerPath = Path.Combine(directory.FullName, "L2Dn.GameServer");
            string dataPackPath = Path.Combine(gameServerPath, "DataPack");
            if (Directory.Exists(dataPackPath))
            {
                return (dataPackPath, Path.Combine(gameServerPath, "Config"));
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the GameServer DataPack from the test output directory.");
    }
}
