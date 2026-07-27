namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Strength of Spirit — kill Gremlins; rewards level 5.
 */
public sealed class Q10073_StrengthOfSpirit: StoryQuest
{
	public Q10073_StrengthOfSpirit()
		: base(DwarvenNewbieIds.QuestStrengthOfSpirit, DwarvenNewbieIds.MonsterGremlin)
	{
	}

	/// <summary>Shared NPC with Q00206Tutorial; avoid competing first-talk on Laferon.</summary>
	protected override bool AutoRegisterNpcFirstTalk => false;

	protected override int NextQuestId => DwarvenNewbieIds.QuestLearningToAutoHunt;
}
