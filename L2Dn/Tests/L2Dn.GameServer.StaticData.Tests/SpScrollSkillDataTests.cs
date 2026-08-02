using System.Xml.Linq;
using FluentAssertions;

namespace L2Dn.GameServer.StaticData.Tests;

public sealed class SpScrollSkillDataTests
{
    [Theory]
    [InlineData(51396, 91664, 10_000)]
    [InlineData(51397, 91665, 100_000)]
    public void Sealed_sp_scroll_skills_grant_sp_and_consume_the_matching_item(
        int skillId,
        int itemId,
        int sp)
    {
        string filePath = FindSkillDataFile();

        XElement skill = XDocument.Load(filePath)
            .Root!
            .Elements("skill")
            .Single(element => (int)element.Attribute("id")! == skillId);

        ((int?)skill.Element("itemConsumeCount")).Should().Be(1);
        ((int?)skill.Element("itemConsumeId")).Should().Be(itemId);
        ((string?)skill.Element("targetType")).Should().Be("SELF");
        ((string?)skill.Element("affectScope")).Should().Be("SINGLE");

        XElement effect = skill.Element("effects")!
            .Elements("effect")
            .Single(element => (string?)element.Attribute("name") == "GiveSp");

        ((int?)effect.Element("sp")).Should().Be(sp);
    }

    private static string FindSkillDataFile()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            string candidate = Path.Combine(
                directory.FullName,
                "L2Dn.GameServer",
                "DataPack",
                "stats",
                "skills",
                "51300-51399.xml");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate SP scroll skill data from the test output directory.");
    }
}
