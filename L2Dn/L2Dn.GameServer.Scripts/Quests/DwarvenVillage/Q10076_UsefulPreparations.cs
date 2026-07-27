using L2Dn.GameServer.Model.Actor;

namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Useful Preparations — hunt Frozen Valley; turn in at Grocer Mion.
 * Rewards use classic jewelry IDs (see NewQuestData 10076); cleans sealed leftovers.
 */
public sealed class Q10076_UsefulPreparations: StoryQuest
{
	public Q10076_UsefulPreparations()
		: base(DwarvenNewbieIds.QuestUsefulPreparations, DwarvenNewbieIds.FrozenValleyMonsters)
	{
	}

	protected override int NextQuestId => DwarvenNewbieIds.QuestTimeToThinkAboutWeapons;

	protected override void OnAccept(Npc? npc, Player player)
	{
		Ep30DwarvenItemCleanup.ReplaceSealedJewelry(player);
		base.OnAccept(npc, player);
	}

	protected override void OnComplete(Player player)
	{
		base.OnComplete(player);
		Ep30DwarvenItemCleanup.ReplaceSealedJewelry(player);
	}
}
