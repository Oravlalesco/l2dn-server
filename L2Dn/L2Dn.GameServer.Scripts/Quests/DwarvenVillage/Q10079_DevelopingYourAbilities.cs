namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Developing Your Abilities — last newbie-chain quest (reward level 20).<br>
 * Does not chain into class-change quests; PlayerClassChange takes over at level 20.
 */
public sealed class Q10079_DevelopingYourAbilities: StoryQuest
{
	public Q10079_DevelopingYourAbilities()
		: base(DwarvenNewbieIds.QuestDevelopingYourAbilities, DwarvenNewbieIds.WesternMiningZoneMonsters)
	{
	}

	protected override int NextQuestId => NoNextQuest;
}
