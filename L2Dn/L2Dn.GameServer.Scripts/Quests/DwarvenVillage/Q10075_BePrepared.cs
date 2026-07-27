namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Be Prepared — hunt monsters in the Frozen Valley.
 */
public sealed class Q10075_BePrepared: StoryQuest
{
	public Q10075_BePrepared()
		: base(DwarvenNewbieIds.QuestBePrepared, DwarvenNewbieIds.FrozenValleyMonsters)
	{
	}

	protected override int NextQuestId => DwarvenNewbieIds.QuestUsefulPreparations;
}
