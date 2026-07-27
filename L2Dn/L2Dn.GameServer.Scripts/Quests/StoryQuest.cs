using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.InstanceManagers;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.Quests;
using L2Dn.GameServer.Model.Quests.NewQuestData;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Network.OutgoingPackets.Quests;
using L2Dn.Geometry;

namespace L2Dn.GameServer.Scripts.Quests;

/**
 * Base class for modern (NewQuestData-driven) story quests.<br>
 * Implements the default ACCEPT / TELEPORT / COMPLETE cycle so thin subclasses only need
 * to declare kill IDs and the next quest in the chain. Override any virtual method for
 * special behaviour (cinematics, instances, custom goals).
 * <p>
 * EP30 clients often lack QuestName/NewQuestData System entries, so instructional text is
 * also pushed via {@link ExShowScreenMessagePacket} (never HTML next to ExQuest TELEPORT).
 */
public class StoryQuest: Quest
{
	/** ExQuest UI event names (must match RequestExQuest*Packet notifyEvent strings). */
	protected const string EventAccept = "ACCEPT";
	protected const string EventTeleport = "TELEPORT";
	protected const string EventComplete = "COMPLETE";

	/** Sentinel for {@link #NextQuestId}: no follow-up quest is offered. */
	protected const int NoNextQuest = 0;

	/** Minimum valid TeleportListData / NPC / item id in NewQuestData (0 and below mean "unset"). */
	private const int UnsetId = 0;

	/** Items granted per qualifying kill when the goal uses goalItemId. */
	private const int GoalItemPerKill = 1;

	/** Floor used when completing a talk goal with a missing/invalid goalCount. */
	private const int MinTalkGoalCount = 1;

	private const int ScreenHintMs = 7000;
	private const int ScreenProgressMs = 4000;

	private readonly int[] _killNpcIds;

	protected StoryQuest(int questId, params int[] killNpcIds)
		: base(questId)
	{
		_killNpcIds = killNpcIds ?? [];
		if (_killNpcIds.Length > 0)
		{
			addKillId(_killNpcIds);
		}
	}

	/**
	 * Quest ID offered after this one is completed. Return {@link #NoNextQuest} to end the chain.
	 */
	protected virtual int NextQuestId => NoNextQuest;

	/**
	 * When true, talking to the end (or start) NPC while STARTED completes a talk-only goal.
	 * Defaults to true when no kill IDs were registered and the goal has no item.
	 */
	protected virtual bool CompletesOnTalk
	{
		get
		{
			NewQuest? data = getQuestData();
			if (data == null)
			{
				return false;
			}

			return _killNpcIds.Length == 0 && data.getGoal().getItemId() <= UnsetId && data.getGoal().getCount() > 0;
		}
	}

	/**
	 * Kill quests auto-finish when the goal is met so combat / auto-attack cannot strand
	 * the player on a dismissed END/TELEPORT dialog. Talk-only quests keep the COMPLETE gate.
	 */
	protected virtual bool AutoCompleteOnGoal => _killNpcIds.Length > 0;

	private const string EventResendGoalDone = "resend_goal_done";

	public override string? onAdvEvent(string @event, Npc? npc, Player? player)
	{
		if (player == null)
		{
			return null;
		}

		switch (@event)
		{
			case EventAccept:
			{
				OnAccept(npc, player);
				break;
			}
			case EventTeleport:
			{
				OnTeleport(player);
				break;
			}
			case EventComplete:
			{
				OnComplete(player);
				break;
			}
			case EventResendGoalDone:
			{
				QuestState? qs = getQuestState(player, false);
				if (qs != null && !qs.isCompleted() && (qs.isCond(QuestCondType.DONE) || IsGoalMet(qs)))
				{
					EnsureGoalDone(qs, player);
					if (AutoCompleteOnGoal)
					{
						OnComplete(player);
					}
					else
					{
						sendEndDialog(player);
						NotifyGoalDone(player);
					}
				}

				break;
			}
		}

		return null;
	}

	/**
	 * When true, ACCEPT opens the START (objective) ExQuest window after the quest starts.
	 * Talk-only quests that finish inside OnAccept should return false to avoid dialog races.
	 */
	protected virtual bool SendStartDialogAfterAccept => true;

	protected virtual void OnAccept(Npc? npc, Player player)
	{
		if (!canStartQuest(player))
		{
			return;
		}

		QuestState questState = getQuestState(player, true)!;
		if (!questState.isStarted() && !questState.isCompleted())
		{
			questState.startQuest();
			if (npc != null)
			{
				giveStoryBuffReward(npc, player);
			}
			else
			{
				giveStoryBuffReward(player);
			}

			// Story UI: show the objective window (same role as tutorial HTML steps).
			if (SendStartDialogAfterAccept)
			{
				player.sendPacket(new ExQuestDialogPacket(getId(), QuestDialogType.START));
			}

			NotifyObjective(player);
		}
	}

	protected virtual void OnTeleport(Player player)
	{
		NewQuest? data = getQuestData();
		if (data == null)
		{
			return;
		}

		NewQuestLocation questLocation = data.getLocation();
		QuestState? questState = getQuestState(player, false);

		// Not started yet: teleport to start (or end) and show accept dialog.
		// Do NOT create a CREATED QuestState here — that confuses RecoverPending / onFirstTalk
		// into showing START while onKill still ignores the inactive quest.
		if (questState == null || questState.isCreated())
		{
			if (!canStartQuest(player))
			{
				return;
			}

			int locationId = questLocation.getStartLocationId() > UnsetId
				? questLocation.getStartLocationId()
				: questLocation.getEndLocationId();

			if (locationId > UnsetId && TryTeleport(player, locationId))
			{
				sendAcceptDialog(player);
			}

			return;
		}

		if (questState.isCond(QuestCondType.STARTED) || questState.isCond(QuestCondType.ACT))
		{
			// Goal already met (e.g. relog mid-combat): finish instead of re-teleporting to hunt.
			if (IsGoalMet(questState))
			{
				EnsureGoalDone(questState, player);
				if (AutoCompleteOnGoal)
				{
					OnComplete(player);
				}
				else
				{
					sendEndDialog(player);
				}

				return;
			}

			int locationId = questLocation.getStartLocationId() > UnsetId
				? questLocation.getStartLocationId()
				: questLocation.getEndLocationId();

			if (locationId > UnsetId)
			{
				player.abortAttack();
				player.setTarget(null);
				TryTeleport(player, locationId);
			}

			return;
		}

		if (questState.isCond(QuestCondType.DONE) && !questState.isCompleted())
		{
			if (AutoCompleteOnGoal)
			{
				OnComplete(player);
				return;
			}

			if (questLocation.getEndLocationId() > UnsetId)
			{
				player.abortAttack();
				player.setTarget(null);
				TryTeleport(player, questLocation.getEndLocationId());
			}

			sendEndDialog(player);
		}
	}

	protected virtual void OnComplete(Player player)
	{
		QuestState? questState = getQuestState(player, false);
		if (questState == null || questState.isCompleted())
		{
			return;
		}

		// Tolerate cond/count desync after combat UI races or partial DB writes.
		if (IsGoalMet(questState) || questState.isCond(QuestCondType.DONE))
		{
			EnsureGoalDone(questState, player);
			questState.exitQuest(false, true);
			rewardPlayer(player);
			OfferNextQuest(player);
		}
	}

	/**
	 * Login / Laferon recovery: finish when the goal is already met; otherwise refresh
	 * objective using the real kill/item count (never a fake 0/N).
	 * @return true if this quest consumed the recovery action
	 */
	public bool RecoverPending(Player player)
	{
		QuestState? questState = getQuestState(player, false);
		if (questState == null || questState.isCompleted())
		{
			return false;
		}

		// CREATED = offered/teleported but never accepted — re-offer ACCEPT, never START.
		if (!questState.isStarted())
		{
			if (!canStartQuest(player))
			{
				return false;
			}

			sendAcceptDialog(player);
			return true;
		}

		if (IsGoalMet(questState) || questState.isCond(QuestCondType.DONE))
		{
			EnsureGoalDone(questState, player);
			OnComplete(player);
			return true;
		}

		player.sendPacket(new ExQuestNotificationPacket(questState));
		player.sendPacket(new ExQuestUiPacket(player));
		player.sendPacket(new ExQuestDialogPacket(getId(), QuestDialogType.START));
		NotifyObjective(player);
		return true;
	}

	protected bool IsGoalMet(QuestState questState)
	{
		NewQuest? data = getQuestData();
		if (data == null)
		{
			return false;
		}

		int goalCount = data.getGoal().getCount();
		return goalCount > 0 && questState.getCount() >= goalCount;
	}

	protected void EnsureGoalDone(QuestState questState, Player player)
	{
		if (questState.isCompleted() || questState.isCond(QuestCondType.DONE))
		{
			return;
		}

		NewQuest? data = getQuestData();
		int goalCount = data != null ? Math.Max(MinTalkGoalCount, data.getGoal().getCount()) : MinTalkGoalCount;
		if (questState.getCount() < goalCount)
		{
			questState.setCount(goalCount);
		}

		questState.setCond(QuestCondType.DONE);
		player.sendPacket(new ExQuestNotificationPacket(questState));
		player.sendPacket(new ExQuestUiPacket(player));
	}

	protected virtual void OfferNextQuest(Player player)
	{
		int nextId = NextQuestId;
		if (nextId <= NoNextQuest)
		{
			return;
		}

		Quest? nextQuest = QuestManager.getInstance().getQuest(nextId);
		if (nextQuest == null)
		{
			return;
		}

		QuestState? nextState = player.getQuestState(nextQuest.Name);
		if ((nextState == null || nextState.isCreated()) && nextQuest.canStartQuest(player))
		{
			player.sendPacket(new ExQuestDialogPacket(nextId, QuestDialogType.ACCEPT));
			NotifyNextQuestOffer(player, nextQuest);
		}
	}

	public override string? onFirstTalk(Npc npc, Player player)
	{
		QuestState? questState = getQuestState(player, false);
		NewQuest? data = getQuestData();

		if (questState != null && !questState.isCompleted() && data != null)
		{
			// CREATED leftovers: offer ACCEPT only (never START — quest is still inactive).
			if (!questState.isStarted())
			{
				if (canStartQuest(player) && IsGoalNpc(npc, data))
				{
					player.sendPacket(new ExQuestDialogPacket(getId(), QuestDialogType.ACCEPT));
				}

				return null;
			}

			if (CompletesOnTalk &&
			    (questState.isCond(QuestCondType.STARTED) || questState.isCond(QuestCondType.ACT)) &&
			    IsGoalNpc(npc, data))
			{
				CompleteGoal(questState, player);
			}

			if (questState.isCond(QuestCondType.DONE))
			{
				player.sendPacket(new ExQuestDialogPacket(getId(), QuestDialogType.END));
				NotifyGoalDone(player);
			}
			else if ((questState.isCond(QuestCondType.STARTED) || questState.isCond(QuestCondType.ACT)) &&
			         questState.getCount() == 0)
			{
				player.sendPacket(new ExQuestDialogPacket(getId(), QuestDialogType.START));
				NotifyObjective(player);
			}
		}
		else if (questState == null && canStartQuest(player) && data != null && IsGoalNpc(npc, data))
		{
			// Offer the quest when talking to its NPC before it was accepted.
			player.sendPacket(new ExQuestDialogPacket(getId(), QuestDialogType.ACCEPT));
		}

		// Never open NPC HTML on the same tick as ExQuestDialog (ACCEPT/START/END):
		// the HTML replaces the quest window immediately — the race Q00206Tutorial avoids
		// with delayed timers. When this quest did not act, leave chat to other scripts
		// (e.g. tutorial on Laferon) or the default talk path.
		return null;
	}

	public override string? onKill(Npc npc, Player? killer, bool isSummon)
	{
		if (killer == null)
		{
			return base.onKill(npc, killer, isSummon);
		}

		QuestState? questState = getQuestState(killer, false);
		// STARTED and ACT both count as active hunting (TELEPORT path may leave ACT).
		if (questState == null ||
		    (!questState.isCond(QuestCondType.STARTED) && !questState.isCond(QuestCondType.ACT)))
		{
			return base.onKill(npc, killer, isSummon);
		}

		NewQuest? data = getQuestData();
		if (data == null)
		{
			return base.onKill(npc, killer, isSummon);
		}

		NewQuestGoal goal = data.getGoal();
		int goalCount = goal.getCount();
		if (goalCount <= 0)
		{
			return base.onKill(npc, killer, isSummon);
		}

		if (goal.getItemId() > UnsetId)
		{
			int itemCount = (int)getQuestItemsCount(killer, goal.getItemId());
			if (itemCount < goalCount)
			{
				giveItems(killer, goal.getItemId(), GoalItemPerKill);
				itemCount = (int)getQuestItemsCount(killer, goal.getItemId());
				questState.setCount(itemCount);
				NotifyProgress(killer, questState.getCount(), goalCount);
			}
		}
		else
		{
			int currentCount = questState.getCount();
			if (currentCount < goalCount)
			{
				questState.setCount(currentCount + 1);
				NotifyProgress(killer, questState.getCount(), goalCount);
			}
		}

		if (questState.getCount() >= goalCount && !questState.isCond(QuestCondType.DONE))
		{
			// Stop combat so ExQuest / teleport UI is not immediately dismissed by auto-attack.
			killer.abortAttack();
			killer.setTarget(null);

			questState.setCond(QuestCondType.DONE);
			killer.sendPacket(new ExQuestNotificationPacket(questState));
			killer.sendPacket(new ExQuestUiPacket(killer));
			NotifyGoalDone(killer);

			if (AutoCompleteOnGoal)
			{
				// Kill quests: finish server-side so a closed dialog cannot strand progress.
				OnComplete(killer);
			}
			else
			{
				sendEndDialog(killer);
				startQuestTimer(EventResendGoalDone, TimeSpan.FromSeconds(3), null, killer);
			}
		}

		return base.onKill(npc, killer, isSummon);
	}

	protected void CompleteGoal(QuestState questState, Player player)
	{
		NewQuest? data = getQuestData();
		if (data == null || questState.isCond(QuestCondType.DONE))
		{
			return;
		}

		int goalCount = Math.Max(MinTalkGoalCount, data.getGoal().getCount());
		questState.setCount(goalCount);
		questState.setCond(QuestCondType.DONE);
		player.sendPacket(new ExQuestNotificationPacket(questState));
		NotifyGoalDone(player);
	}

	/// <summary>Re-show objective hint (recovery / login). Safe with ExQuest TELEPORT open.</summary>
	public void SendObjectiveHint(Player player)
	{
		NotifyObjective(player);
	}

	protected void NotifyObjective(Player player)
	{
		NewQuest? data = getQuestData();
		if (data == null)
		{
			return;
		}

		NewQuestGoal goal = data.getGoal();
		string goalText = string.IsNullOrEmpty(goal.getMessage()) ? "objective" : goal.getMessage();
		QuestState? questState = getQuestState(player, false);
		int current = questState?.getCount() ?? 0;
		string text = goal.getCount() > 0
			? $"{data.getName()}: {goalText} ({current}/{goal.getCount()})"
			: $"{data.getName()}: {goalText}";

		player.sendPacket(new ExShowScreenMessagePacket(text, ExShowScreenMessagePacket.TOP_CENTER, ScreenHintMs));
	}

	protected void NotifyProgress(Player player, int current, int goalCount)
	{
		NewQuest? data = getQuestData();
		string name = data?.getName() ?? Name;
		string goalText = data?.getGoal().getMessage() ?? "progress";
		string text = $"{name}: {goalText} ({current}/{goalCount})";
		player.sendPacket(new ExShowScreenMessagePacket(text, ExShowScreenMessagePacket.TOP_CENTER, ScreenProgressMs));
	}

	protected void NotifyGoalDone(Player player)
	{
		NewQuest? data = getQuestData();
		string name = data?.getName() ?? Name;
		string text = AutoCompleteOnGoal
			? $"{name}: objective complete — claiming reward / next quest."
			: $"{name}: objective complete — open Quest UI and press Complete (or talk to Laferon).";
		player.sendPacket(new ExShowScreenMessagePacket(text, ExShowScreenMessagePacket.TOP_CENTER, ScreenHintMs));
	}

	protected static void NotifyNextQuestOffer(Player player, Quest nextQuest)
	{
		string name = nextQuest.getQuestData()?.getName() ?? nextQuest.Name;
		player.sendPacket(new ExShowScreenMessagePacket(
			$"Next story quest available: {name} — Accept it in the Quest window.",
			ExShowScreenMessagePacket.TOP_CENTER, ScreenHintMs));
	}

	protected bool IsGoalNpc(Npc npc, NewQuest data)
	{
		int npcId = npc.getId();
		if (data.getEndNpcId() > UnsetId && npcId == data.getEndNpcId())
		{
			return true;
		}

		if (data.getStartNpcId() > UnsetId && npcId == data.getStartNpcId())
		{
			return true;
		}

		return false;
	}

	protected bool TryTeleport(Player player, int teleportId)
	{
		TeleportListHolder? teleport = TeleportListData.getInstance().getTeleport(teleportId);
		if (teleport == null)
		{
			LOGGER.Warn(GetType().Name + ": missing TeleportListData id " + teleportId);
			return false;
		}

		Location3D loc3d = teleport.getLocation();
		return teleportToQuestLocation(player, new Location(loc3d, player.getHeading()));
	}
}
