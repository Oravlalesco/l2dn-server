namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Time to Think About Weapons — hunt Western Mining Zone.
 */
public sealed class Q10077_TimeToThinkAboutWeapons: StoryQuest
{
	public Q10077_TimeToThinkAboutWeapons()
		: base(DwarvenNewbieIds.QuestTimeToThinkAboutWeapons, DwarvenNewbieIds.WesternMiningZoneMonsters)
	{
	}

	protected override int NextQuestId => DwarvenNewbieIds.QuestWatchOut;
}
