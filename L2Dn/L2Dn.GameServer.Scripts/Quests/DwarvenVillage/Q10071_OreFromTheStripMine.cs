using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Quests;
using L2Dn.GameServer.Model.Quests.NewQuestData;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Scripts.Quests.DwarvenVillage;

/**
 * Ore from the Strip Mine — talk to Foreman Laferon.
 * First quest of the dwarven newbie story chain.
 * Offered by Q00206Tutorial after reward_2 (specificStart); does not own Laferon's first-talk.
 * <p>
 * specificStart only suppresses EnterWorld auto-offer; ACCEPT is gated here via canStartQuest
 * so a forged ExQuestAccept packet cannot skip the tutorial (memoState must be >= 5).
 */
public sealed class Q10071_OreFromTheStripMine: StoryQuest
{
	public Q10071_OreFromTheStripMine()
		: base(DwarvenNewbieIds.QuestOreFromTheStripMine)
	{
		addCondStart(HasFinishedDwarvenTutorial, (string?)null);
	}

	/// <summary>
	/// Q00206Tutorial owns Foreman Laferon first-talk during the strip-mine phase.
	/// </summary>
	protected override bool AutoRegisterNpcFirstTalk => false;

	protected override int NextQuestId => DwarvenNewbieIds.QuestNewLifesLessons;

	/// <summary>
	/// Skip START window: this quest finishes inside OnAccept and offers 10072 next.
	/// </summary>
	protected override bool SendStartDialogAfterAccept => false;

	/// <summary>
	/// Talk-only goal without first-talk: accepting counts as the talk.
	/// Completes immediately (no END dialog) so the client does not race ACCEPT+END
	/// against Laferon's HTML and leave the chain stuck DONE without COMPLETE.
	/// </summary>
	protected override void OnAccept(Npc? npc, Player player)
	{
		base.OnAccept(npc, player);

		QuestState? questState = getQuestState(player, false);
		if (questState == null || questState.isCompleted())
		{
			return;
		}

		if (!questState.isCond(QuestCondType.DONE))
		{
			CompleteGoal(questState, player);
		}

		OnComplete(player);
	}

	/**
	 * Server-side gate for specificStart 10071.
	 * Uses the raw memo var (getMemoState returns 0 when the tutorial is not STARTED).
	 */
	private static bool HasFinishedDwarvenTutorial(Player player)
	{
		if (Config.Character.DISABLE_TUTORIAL)
		{
			return true;
		}

		QuestState? tutorial = player.getQuestState(DwarvenNewbieIds.TutorialQuestName);
		return tutorial != null &&
		       tutorial.getInt("memoState") >= DwarvenNewbieIds.TutorialFinishedMemoState;
	}
}
