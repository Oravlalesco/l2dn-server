namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Watch Out — hunt Western Mining Zone.
 */
public sealed class Q10078_WatchOut: StoryQuest
{
	public Q10078_WatchOut()
		: base(DwarvenNewbieIds.QuestWatchOut, DwarvenNewbieIds.WesternMiningZoneMonsters)
	{
	}

	protected override int NextQuestId => DwarvenNewbieIds.QuestDevelopingYourAbilities;
}
