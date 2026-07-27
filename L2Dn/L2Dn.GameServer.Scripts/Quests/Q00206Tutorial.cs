using L2Dn.GameServer.Dto;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.InstanceManagers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Annotations;
using L2Dn.GameServer.Model.Events.Impl.Players;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.Html;
using L2Dn.GameServer.Model.Quests;
using L2Dn.GameServer.Model.Quests.NewQuestData;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Network.OutgoingPackets.Quests;
using L2Dn.GameServer.Scripts.Quests.DwarvenVillage;
using L2Dn.Model;
using L2Dn.Model.Enums;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Scripts.Quests;

public sealed class Q00206Tutorial: Quest
{
	private const string TUTORIAL_BYPASS = "Quest Q00206Tutorial ";
    private const int QUESTION_MARK_ID_1 = 1;
    private const int QUESTION_MARK_ID_2 = 5;

    // Items
    private const int BLUE_GEM = 6353;
    private readonly ItemHolder SOULSHOT_REWARD = new ItemHolder(91927, 200);
    private readonly ItemHolder SPIRITSHOT_REWARD = new ItemHolder(91928, 100);
    private readonly ItemHolder SCROLL_OF_ESCAPE = new ItemHolder(10650, 5);
    private readonly ItemHolder WIND_WALK_POTION = new ItemHolder(49036, 5);

    // Npcs
    private const int NEWBIE_HELPER = 30530;
    private const int SUPERVISOR = 30528;

    // Monsters
    private const int GREMLIN = 18342;

    public Q00206Tutorial()
        : base(206)
    {
	    // TODO: think about state machine for quests
        addStartNpc(NEWBIE_HELPER, SUPERVISOR); // required for QuestLink / "Quest" button → notifyTalk
        addTalkId(NEWBIE_HELPER, SUPERVISOR);
        addFirstTalkId(NEWBIE_HELPER, SUPERVISOR);
        addKillId(GREMLIN);
        registerQuestItems(BLUE_GEM);
    }

    /// <summary>
    /// Handles the HTML "Quest" button (npc_%objectId%_Quest). At memoState 4 this is the strip-mine
    /// handoff and must run the same path as reward_2 (soulshots + offer 10071).
    /// </summary>
    public override string? onTalk(Npc npc, Player talker)
    {
	    QuestState? qs = getQuestState(talker, false);
	    if (qs == null)
	    {
		    return getNoQuestMsg(talker);
	    }

	    if (npc.getId() == SUPERVISOR && qs.isMemoState(4) && !talker.isSimulatingTalking())
	    {
		    notifyEvent("reward_2", npc, talker);
		    return null;
	    }

	    // Same dialogue as first-talk so the Quest button is never a dead end.
	    return onFirstTalk(npc, talker);
    }

    public override string? onFirstTalk(Npc npc, Player player)
    {
		QuestState? qs = getQuestState(player, false);
		if (qs != null)
		{
			// start newbie helpers
			if (npc.getId() == NEWBIE_HELPER)
			{
				if (hasQuestItems(player, BLUE_GEM))
					qs.setMemoState(3);

				switch (qs.getMemoState())
				{
					case 0:
					case 1:
					{
						player.sendPacket(TutorialCloseHtmlPacket.STATIC_PACKET);
						player.getClient()?.HtmlActionValidator.ClearActions(HtmlActionScope.TUTORIAL_HTML);
						qs.setMemoState(2);
						return "tutorial_05_fighter.html";
					}

					case 2:
					{
						return "tutorial_05_fighter_back.html";
					}

					case 3:
					{
						player.sendPacket(TutorialCloseHtmlPacket.STATIC_PACKET);
						player.getClient()?.HtmlActionValidator.ClearActions(HtmlActionScope.TUTORIAL_HTML);
						qs.setMemoState(4);
						takeItems(player, BLUE_GEM, -1);
						giveItems(player, SCROLL_OF_ESCAPE);
						giveItems(player, WIND_WALK_POTION);
						giveItems(player, SOULSHOT_REWARD);
						playTutorialVoice(player, "tutorial_voice_026");
						return npc.getId() + "-2.html";
					}
					case 4:
					{
						return npc.getId() + "-4.html";
					}
					case 5:
					case 6:
					{
						return npc.getId() + "-5.html";
					}
				}
			}

			// Foreman Laferon — Essence: stay in strip mine for NewQuest 10071–10073
			switch (qs.getMemoState())
			{
				case 0:
				case 1:
				case 2:
				case 3:
				{
					return npc.getId() + "-1.html";
				}
				case 4:
				{
					return npc.getId() + "-2.html";
				}
				case 5:
				case 6:
				{
					// Pending ExQuest must not race Laferon's farewell HTML (window flashes <1s).
					if (npc.getId() == SUPERVISOR && OfferPendingDwarvenStoryDialog(player))
					{
						return null;
					}

					return npc.getId() + "-4.html";
				}
			}
		}

		return npc.getId() + "-1.html";
    }

    public override string? onAdvEvent(string ev, Npc? npc, Player? player)
    {
        if (player is null)
            return null;

        QuestState? qs = getQuestState(player, false);
        if (qs == null)
            return null;

        if (ev == "start_newbie_tutorial")
        {
            if (qs.getMemoState() < 4)
            {
	            qs.startQuest();
	            qs.setMemoState(1);
                showOnScreenMsg(player, NpcStringId.TALK_TO_NEWBIE_HELPER, ExShowScreenMessagePacket.TOP_CENTER, 5000);
                playTutorialVoice(player, "tutorial_voice_001i");
                showTutorialHtml(player, "tutorial_dwarven_fighter001.html");
            }
        }
        else if (ev == "tutorial_02.html" || ev == "tutorial_03.html")
        {
            if (qs.isMemoState(1))
                showTutorialHtml(player, ev);
        }
        else if (ev == "question_mark_1")
        {
            if (qs.isMemoState(1))
            {
                player.sendPacket(new TutorialShowQuestionMarkPacket(QUESTION_MARK_ID_1, 0));
                player.sendPacket(TutorialCloseHtmlPacket.STATIC_PACKET);
                player.getClient()?.HtmlActionValidator.ClearActions(HtmlActionScope.TUTORIAL_HTML);
            }
        }
        else if (ev == "reward_2")
        {
            if (qs.isMemoState(4))
            {
                qs.setMemoState(5);
                if (player.isMageClass() && player.getRace() != Race.ORC)
                {
                    giveItems(player, SPIRITSHOT_REWARD);
                    playTutorialVoice(player, "tutorial_voice_027");
                }
                else
                {
                    giveItems(player, SOULSHOT_REWARD);
                    playTutorialVoice(player, "tutorial_voice_026");
                }

                // Delay ExQuestDialog so it is not raced against Laferon's open HTML.
                startQuestTimer("offer_dwarven_story", TimeSpan.FromSeconds(1), null, player);
            }
        }
        else if (ev == "offer_dwarven_story")
        {
            OfferPendingDwarvenStoryDialog(player);
        }
        else if (ev == "close_tutorial")
        {
            player.sendPacket(TutorialCloseHtmlPacket.STATIC_PACKET);
            player.getClient()?.HtmlActionValidator.ClearActions(HtmlActionScope.TUTORIAL_HTML);
        }

        return null;
    }

    public override string? onKill(Npc npc, Player? killer, bool isSummon)
    {
        if (killer != null)
        {
            QuestState? qs = getQuestState(killer, false);
            if (qs != null && qs.getMemoState() < 3 && !hasQuestItems(killer, BLUE_GEM) && getRandom(100) < 50)
            {
                giveItems(killer, BLUE_GEM, 1);
                qs.setMemoState(3);
                playSound(killer, "ItemSound.quest_tutorial");
                playTutorialVoice(killer, "tutorial_voice_013");
                killer.sendPacket(new TutorialShowQuestionMarkPacket(QUESTION_MARK_ID_2, 0));
            }
        }

        return base.onKill(npc, killer, isSummon);
    }

    [SubscribeEvent(SubscriptionType.GlobalPlayers)]
    public void PlayerPressTutorialMark(OnPlayerPressTutorialMark ev)
    {
	    QuestState? qs = getQuestState(ev.getPlayer(), false);
	    if (qs == null)
		    return;

	    switch (ev.getMarkId())
	    {
		    case QUESTION_MARK_ID_1:
		    {
			    if (qs.isMemoState(1))
			    {
				    showOnScreenMsg(ev.getPlayer(), NpcStringId.TALK_TO_NEWBIE_HELPER,
					    ExShowScreenMessagePacket.TOP_CENTER, 5000);

				    addRadar(ev.getPlayer(), 108567, -173994, -406);
				    showTutorialHtml(ev.getPlayer(), "tutorial_04.html");
				    playTutorialVoice(ev.getPlayer(), "tutorial_voice_007");
			    }

			    break;
		    }

		    case QUESTION_MARK_ID_2:
		    {
			    if (qs.isMemoState(3))
			    {
				    addRadar(ev.getPlayer(), 108567, -173994, -406);
				    showTutorialHtml(ev.getPlayer(), "tutorial_06.html");
			    }

			    break;
		    }
	    }
    }

    [SubscribeEvent(SubscriptionType.GlobalPlayers)]
    public void PlayerBypass(OnPlayerBypass ev)
    {
	    Player player = ev.getPlayer();
	    if (ev.getCommand().StartsWith(TUTORIAL_BYPASS))
	    {
		    string command = ev.getCommand().Replace(TUTORIAL_BYPASS, "");
		    notifyEvent(command, null, player);
	    }
    }

    [SubscribeEvent(SubscriptionType.GlobalPlayers)]
    public void PlayerLogin(OnPlayerLogin ev)
    {
        if (Config.Character.DISABLE_TUTORIAL)
            return;

        Player player = ev.getPlayer();
        if (player.getClassId() != CharacterClass.DWARVEN_FIGHTER)
            return;

        QuestState? qs = getQuestState(player, true);
        if (qs == null)
            return;

        // Early tutorial: classic mark/timer.
        if (qs.getMemoState() < 4 && player.getLevel() <= 6)
        {
            startQuestTimer("start_newbie_tutorial", TimeSpan.FromSeconds(5), null, player);
            return;
        }

        // Story chain recovery: EnterWorld skips specificStart (10071) and never re-sends END
        // for DONE-but-not-COMPLETED states, so re-offer pending ExQuest dialogs after login.
        if (qs.getMemoState() >= 5)
        {
            Ep30DwarvenItemCleanup.ReplaceSealedJewelry(player);
            startQuestTimer("offer_dwarven_story", TimeSpan.FromSeconds(2), null, player);
        }
    }

    /// <summary>
    /// Re-offer / finish the first pending dwarven NewQuest for 10071–10079.
    /// Returns true when an ExQuest action was taken (caller should not open NPC HTML).
    /// </summary>
    private static bool OfferPendingDwarvenStoryDialog(Player player)
    {
        Ep30DwarvenItemCleanup.ReplaceSealedJewelry(player);

        int[] chain =
        [
            DwarvenNewbieIds.QuestOreFromTheStripMine,
            DwarvenNewbieIds.QuestNewLifesLessons,
            DwarvenNewbieIds.QuestStrengthOfSpirit,
            DwarvenNewbieIds.QuestLearningToAutoHunt,
            DwarvenNewbieIds.QuestBePrepared,
            DwarvenNewbieIds.QuestUsefulPreparations,
            DwarvenNewbieIds.QuestTimeToThinkAboutWeapons,
            DwarvenNewbieIds.QuestWatchOut,
            DwarvenNewbieIds.QuestDevelopingYourAbilities,
        ];

        foreach (int questId in chain)
        {
            Quest? quest = QuestManager.getInstance().getQuest(questId);
            if (quest == null)
            {
                continue;
            }

            QuestState? state = player.getQuestState(quest.Name);
            if (state == null)
            {
                if (quest.canStartQuest(player))
                {
                    player.sendPacket(new ExQuestDialogPacket(questId, QuestDialogType.ACCEPT));
                    return true;
                }

                return false;
            }

            if (state.isCompleted())
            {
                continue;
            }

            if (questId == DwarvenNewbieIds.QuestLearningToAutoHunt)
            {
                takeItems(player, DwarvenNewbieIds.ItemHerbRoots, -1);
            }

            // Prefer StoryQuest recovery: finishes when goal already met (count/DONE),
            // otherwise re-shows START with the real progress (never fake 0/N).
            if (quest is StoryQuest storyQuest)
            {
                storyQuest.RecoverPending(player);

                // Strip-mine exit only if 10074 is still hunting (goal not met yet).
                // Do not re-TELEPORT after kills — combat already left the player in Frozen Valley
                // and a second TELEPORT/START feels like "kill 3 again".
                QuestState? after = player.getQuestState(quest.Name);
                if (questId == DwarvenNewbieIds.QuestLearningToAutoHunt &&
                    after != null && !after.isCompleted() && after.getCount() == 0 &&
                    !after.isCond(QuestCondType.DONE))
                {
                    quest.notifyEvent("TELEPORT", null, player);
                }

                return true;
            }

            if (state.isCond(QuestCondType.DONE))
            {
                quest.notifyEvent("COMPLETE", null, player);
                return true;
            }

            player.sendPacket(new ExQuestNotificationPacket(state));
            player.sendPacket(new ExQuestUiPacket(player));
            player.sendPacket(new ExQuestDialogPacket(questId, QuestDialogType.START));
            return true;
        }

        return false;
    }

    private void showTutorialHtml(Player player, string html)
    {
        HtmlContent htmlContent = HtmlContent.LoadFromText(getHtm(player, html), player);
        player.sendPacket(new TutorialShowHtmlPacket(htmlContent));
    }

    private void playTutorialVoice(Player player, string voice)
    {
        player.sendPacket(new PlaySoundPacket(2, voice, 0, 0, player.getX(), player.getY(), player.getZ()));
    }
}