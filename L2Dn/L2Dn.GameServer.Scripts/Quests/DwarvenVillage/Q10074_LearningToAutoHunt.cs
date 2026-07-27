using L2Dn.GameServer.Model.Actor;

namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Learning to Auto-hunt — kill Longtail Keltirs in Frozen Valley.
 * Uses kill-count (not Herb Roots 98464): that item id is missing on EP30 clients
 * and InventoryUpdate crashes when the unknown quest item is shown.
 */
public sealed class Q10074_LearningToAutoHunt: StoryQuest
{
	public Q10074_LearningToAutoHunt()
		: base(DwarvenNewbieIds.QuestLearningToAutoHunt, DwarvenNewbieIds.HerbRootMonsters)
	{
	}

	/// <summary>Shared NPC with Q00206Tutorial; avoid competing first-talk on Laferon.</summary>
	protected override bool AutoRegisterNpcFirstTalk => false;

	protected override int NextQuestId => DwarvenNewbieIds.QuestBePrepared;

	protected override void OnAccept(Npc? npc, Player player)
	{
		// Strip any leftover Herb Roots from older goalItemId builds (client-unsafe id).
		takeItems(player, DwarvenNewbieIds.ItemHerbRoots, -1);
		base.OnAccept(npc, player);
		// Essence-style: move to Frozen Valley hunt zone without relying on client TELEPORT UI.
		OnTeleport(player);
	}
}
