namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * New Life's Lessons — kill Gremlins.
 */
public sealed class Q10072_NewLifesLessons: StoryQuest
{
	public Q10072_NewLifesLessons()
		: base(DwarvenNewbieIds.QuestNewLifesLessons, DwarvenNewbieIds.MonsterGremlin)
	{
	}

	/// <summary>Shared NPC with Q00206Tutorial; avoid competing first-talk on Laferon.</summary>
	protected override bool AutoRegisterNpcFirstTalk => false;

	protected override int NextQuestId => DwarvenNewbieIds.QuestStrengthOfSpirit;
}
