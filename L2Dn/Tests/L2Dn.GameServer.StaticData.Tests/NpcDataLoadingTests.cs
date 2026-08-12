using FluentAssertions;
using L2Dn.GameServer.Configuration;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor.Templates;
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
        templates.SelectMany(template => template.getAISkills(AISkillScope.HEAL))
            .Should().BeEmpty("Talking Island has no heal-capable base mob for the Survival profile");
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
