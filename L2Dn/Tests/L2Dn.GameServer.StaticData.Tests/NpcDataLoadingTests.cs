using FluentAssertions;
using L2Dn.GameServer.Configuration;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor.Templates;

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
