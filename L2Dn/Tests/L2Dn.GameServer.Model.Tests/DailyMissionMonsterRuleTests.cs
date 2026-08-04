using FluentAssertions;
using L2Dn.GameServer.Model.DailyMissions;
using L2Dn.GameServer.Model.Spawns;

namespace L2Dn.GameServer.Model.Tests;

public class DailyMissionMonsterRuleTests
{
    [Theory]
    [InlineData(80, 75, false)]
    [InlineData(80, 76, true)]
    [InlineData(80, 99, true)]
    public void Generic_hunting_excludes_five_or_more_levels_below_without_an_upper_limit(
        int playerLevel, int monsterLevel, bool expected)
    {
        DailyMissionMonsterRule.MatchesGenericMonsterLevel(playerLevel, monsterLevel).Should().Be(expected);
    }

    [Fact]
    public void Id_or_area_mode_matches_once_even_when_both_dimensions_match()
    {
        DailyMissionMonsterRule.MatchesTarget(DailyMissionMonsterTargetMode.NPC_IDS_OR_SPAWN_AREAS, 21872,
            new HashSet<int> { 21872 }, new HashSet<string> { "varka_silenos_barracks" },
            new HashSet<string> { "varka_silenos_barracks" }).Should().BeTrue();
    }

    [Fact]
    public void Spawn_area_tags_inherit_and_can_be_excluded_at_a_child_scope()
    {
        IReadOnlySet<string> parent = DailyMissionAreaTags.Resolve(null, "giants_cave,shared", null);
        IReadOnlySet<string> child = DailyMissionAreaTags.Resolve(parent, "lower_floor", "shared");

        child.Should().BeEquivalentTo("giants_cave", "lower_floor");
    }
}
