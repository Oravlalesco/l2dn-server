using L2Dn.GameServer.Dto;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Annotations;
using L2Dn.GameServer.Model.Events.Impl.Players;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.Html;
using L2Dn.GameServer.Model.Quests;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.Geometry;
using L2Dn.Model;
using L2Dn.Model.Enums;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Scripts.Quests;

public sealed class Q00206Tutorial: Quest
{
    private const string TUTORIAL_BYPASS = "Quest Q00206Tutorial ";
    private const string GEM_REWARD_FIGHTER = "gem_reward_fighter";
    private const string GEM_REWARD_MYSTIC = "gem_reward_mystic";
    private const string GEM_REWARD_EXPLORER = "gem_reward_explorer";
    private const int QUESTION_MARK_ID_1 = 1;
    private const int QUESTION_MARK_ID_2 = 5;
    private const int QUESTION_MARK_ID_3 = 28;

    // Items
    private const int BLUE_GEM = 6353;
    private const long COMBAT_GEM_ADENA_REWARD = 75_000;
    private const long EXPLORER_GEM_ADENA_REWARD = 150_000;
    private readonly ItemHolder SOULSHOT_REWARD = new ItemHolder(91927, 200);
    private readonly ItemHolder SPIRITSHOT_REWARD = new ItemHolder(91928, 100);
    private readonly ItemHolder GEM_FIGHTER_SOULSHOT_REWARD = new ItemHolder(91927, 300);
    private readonly ItemHolder GEM_MYSTIC_SPIRITSHOT_REWARD = new ItemHolder(91928, 150);
    private readonly ItemHolder GEM_HP_POTION_REWARD = new ItemHolder(1060, 10);
    private readonly ItemHolder GEM_EXPLORER_HP_POTION_REWARD = new ItemHolder(1060, 20);
    private readonly ItemHolder GEM_ESCAPE_SCROLL_REWARD = new ItemHolder(736, 3);

    // Human fighter tutorial
    private const int HUMAN_FIGHTER_HELPER = 30009;
    private const int GRAND_MASTER_ROIEN = 30008;

    // Human mystic tutorial
    private const int HUMAN_MYSTIC_HELPER = 30019;
    private const int GRAND_MAGISTER_GALLINT = 30017;

    // Elven tutorial
    private const int ELVEN_HELPER = 30400;
    private const int NERUPA = 30370;

    // Dark Elven tutorial
    private const int DARK_ELVEN_HELPER = 30131;
    private const int MITRAELL = 30129;

    // Orc tutorial
    private const int ORC_HELPER = 30575;
    private const int VULKUS = 30573;

    // Dwarven tutorial
    private const int DWARVEN_HELPER = 30530;
    private const int LAFERON = 30528;

    // Kamael tutorial
    private const int KAMAEL_HELPER = 34108;
    private const int RAGNIR = 34109;

    private static readonly TutorialRoute HUMAN_FIGHTER_ROUTE = new(
        HUMAN_FIGHTER_HELPER,
        GRAND_MASTER_ROIEN,
        new Location(-71424, 258336, -3109, 42000),
        new Location(-84081, 243227, -3723, 9000),
        "Grand Master Roien",
        "tutorial_new_character001.html");

    private static readonly TutorialRoute HUMAN_MYSTIC_ROUTE = new(
        HUMAN_MYSTIC_HELPER,
        GRAND_MAGISTER_GALLINT,
        new Location(-91036, 248044, -3568, 6000),
        new Location(-84081, 243227, -3723, 9000),
        "Grand Magister Gallint",
        "tutorial_new_character001.html");

    private static readonly TutorialRoute ELVEN_ROUTE = new(
        ELVEN_HELPER,
        NERUPA,
        new Location(46112, 41200, -3504, 17000),
        new Location(45475, 48359, -3060, 49152),
        "Nerupa",
        "tutorial_new_character001.html");

    private static readonly TutorialRoute DARK_ELVEN_ROUTE = new(
        DARK_ELVEN_HELPER,
        MITRAELL,
        new Location(28384, 11056, -4233, 32000),
        new Location(12111, 16686, -4582, 63240),
        "Dark Elf Chief Mitraell",
        "tutorial_new_character001.html");

    private static readonly TutorialRoute ORC_ROUTE = new(
        ORC_HELPER,
        VULKUS,
        new Location(-56736, -113680, -672, 0),
        new Location(-45032, -113598, -192, 32768),
        "Flame Guardian Vulkus",
        "tutorial_new_character001.html");

    private static readonly TutorialRoute DWARVEN_ROUTE = new(
        DWARVEN_HELPER,
        LAFERON,
        new Location(108567, -173994, -406, 38000),
        new Location(115575, -178014, -904, 9808),
        "Foreman Laferon",
        "tutorial_dwarven_fighter001.html");

    private static readonly TutorialRoute KAMAEL_ROUTE = new(
        KAMAEL_HELPER,
        RAGNIR,
        new Location(-124748, 38078, 1208, 0),
        new Location(-118075, 45059, 368, 17358),
        "Elder Ragnir",
        "tutorial_new_character001.html");

    private static readonly int[] NEWBIE_HELPERS =
    [
        HUMAN_FIGHTER_HELPER,
        HUMAN_MYSTIC_HELPER,
        ELVEN_HELPER,
        DARK_ELVEN_HELPER,
        ORC_HELPER,
        DWARVEN_HELPER,
        KAMAEL_HELPER,
    ];

    private static readonly int[] SUPERVISORS =
    [
        GRAND_MASTER_ROIEN,
        GRAND_MAGISTER_GALLINT,
        NERUPA,
        MITRAELL,
        VULKUS,
        LAFERON,
        RAGNIR,
    ];

    // Monsters
    private const int GREMLIN = 18342;

    public Q00206Tutorial()
        : base(206)
    {
        // TODO: think about state machine for quests
        addTalkId(NEWBIE_HELPERS);
        addTalkId(SUPERVISORS);
        addFirstTalkId(NEWBIE_HELPERS);
        addFirstTalkId(SUPERVISORS);
        addKillId(GREMLIN);
        registerQuestItems(BLUE_GEM);
    }

    private static TutorialRoute? getTutorialRoute(Player player)
    {
        return player.getRace() switch
        {
            Race.HUMAN => player.isMageClass() ? HUMAN_MYSTIC_ROUTE : HUMAN_FIGHTER_ROUTE,
            Race.ELF => ELVEN_ROUTE,
            Race.DARK_ELF => DARK_ELVEN_ROUTE,
            Race.ORC => ORC_ROUTE,
            Race.DWARF => DWARVEN_ROUTE,
            Race.KAMAEL => KAMAEL_ROUTE,
            _ => null,
        };
    }

    public override string? onFirstTalk(Npc npc, Player player)
    {
        TutorialRoute? route = getTutorialRoute(player);
        QuestState? qs = getQuestState(player, false);
        if (route == null || qs == null)
            return null;

        if (npc.getId() == route.NewbieHelperId)
        {
            if (qs.getMemoState() == 0)
            {
                qs.startQuest();
                qs.setMemoState(1);
            }

            if (qs.getMemoState() < 3 && hasQuestItems(player, BLUE_GEM))
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
                    return "30530-2.html";
                }
                case 4:
                {
                    return getHelperFollowupHtml(route);
                }
                case 5:
                case 6:
                {
                    return getTutorialCompleteHtml();
                }
            }
        }

        if (npc.getId() != route.SupervisorId)
            return null;

        return qs.getMemoState() switch
        {
            0 or 1 or 2 or 3 => getSupervisorIntroHtml(route),
            4 => getSupervisorRewardHtml(route),
            5 or 6 => getSupervisorCompleteHtml(route),
            _ => getSupervisorIntroHtml(route),
        };
    }

    public override string? onAdvEvent(string ev, Npc? npc, Player? player)
    {
        if (player is null)
            return null;

        QuestState? qs = getQuestState(player, false);
        if (qs == null)
            return null;

        TutorialRoute? route = getTutorialRoute(player);
        if (route == null)
            return null;

        if (ev == "start_newbie_tutorial")
        {
            if (qs.getMemoState() == 0)
            {
                qs.startQuest();
                qs.setMemoState(1);
            }

            if (qs.isMemoState(1))
            {
                showOnScreenMsg(player, NpcStringId.TALK_TO_NEWBIE_HELPER, ExShowScreenMessagePacket.TOP_CENTER, 5000);
                playTutorialVoice(player, "tutorial_voice_001i");
                showTutorialHtml(player, route.WelcomeHtml);
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
        else if (ev == GEM_REWARD_FIGHTER || ev == GEM_REWARD_MYSTIC || ev == GEM_REWARD_EXPLORER)
        {
            if (giveBlueGemReward(ev, qs, player))
                return getHelperFollowupHtml(route);
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

                // There is no html window.
                player.sendPacket(new TutorialShowQuestionMarkPacket(QUESTION_MARK_ID_3, 0));
                player.teleToLocation(route.NewbieGuideLocation);
            }
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
            if (getTutorialRoute(killer) != null && qs != null && qs.getMemoState() < 3 &&
                !hasQuestItems(killer, BLUE_GEM) && getRandom(100) < 50)
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

    private bool giveBlueGemReward(string rewardEvent, QuestState qs, Player player)
    {
        if (getTutorialRoute(player) == null || !qs.isMemoState(3) || !hasQuestItems(player, BLUE_GEM) ||
            !takeItems(player, BLUE_GEM, -1))
            return false;

        // Advance before granting items so repeated bypass requests cannot claim another kit.
        qs.setMemoState(4);
        giveStoryBuffReward(player);

        long adenaReward;
        switch (rewardEvent)
        {
            case GEM_REWARD_FIGHTER:
            {
                giveItems(player, GEM_HP_POTION_REWARD);
                giveItems(player, GEM_FIGHTER_SOULSHOT_REWARD);
                adenaReward = COMBAT_GEM_ADENA_REWARD;
                break;
            }
            case GEM_REWARD_MYSTIC:
            {
                giveItems(player, GEM_HP_POTION_REWARD);
                giveItems(player, GEM_MYSTIC_SPIRITSHOT_REWARD);
                adenaReward = COMBAT_GEM_ADENA_REWARD;
                break;
            }
            case GEM_REWARD_EXPLORER:
            {
                giveItems(player, GEM_EXPLORER_HP_POTION_REWARD);
                giveItems(player, GEM_ESCAPE_SCROLL_REWARD);
                adenaReward = EXPLORER_GEM_ADENA_REWARD;
                break;
            }
            default:
            {
                return false;
            }
        }

        player.addAdena(Name, adenaReward, null, true);
        playTutorialVoice(player, "tutorial_voice_026");
        return true;
    }

    private static string getHelperFollowupHtml(TutorialRoute route)
    {
        return "<html><body>Newbie Helper:<br>" +
            "Your chosen supplies have been delivered. Use them well.<br>" +
            "Now speak with <font color=\"LEVEL\">" + route.SupervisorName +
            "</font> nearby. Your instructor will send you to the Newbie Guide." +
            "</body></html>";
    }

    private static string getTutorialCompleteHtml()
    {
        return "<html><body>Newbie Helper:<br>" +
            "I've taught you all I can. Continue your training with the Newbie Guide and use your supplies well." +
            "</body></html>";
    }

    private static string getSupervisorIntroHtml(TutorialRoute route)
    {
        return "<html><body>" + route.SupervisorName + ":<br>" +
            "First, learn the basic controls from the <font color=\"LEVEL\">Newbie Helper</font> beside me. " +
            "Recover the Blue Gemstone from a Gremlin and return to the helper." +
            "</body></html>";
    }

    private static string getSupervisorRewardHtml(TutorialRoute route)
    {
        return "<html><body>" + route.SupervisorName + ":<br>" +
            "You completed the Newbie Helper's task. I will now send you to the nearby settlement.<br>" +
            "<Button ALIGN=LEFT ICON=\"NORMAL\" action=\"bypass -h Quest Q00206Tutorial reward_2\">" +
            "Go to the Newbie Guide</Button>" +
            "</body></html>";
    }

    private static string getSupervisorCompleteHtml(TutorialRoute route)
    {
        return "<html><body>" + route.SupervisorName + ":<br>" +
            "Continue your training with the <font color=\"LEVEL\">Newbie Guide</font>. Good luck on your journey!" +
            "</body></html>";
    }

    [SubscribeEvent(SubscriptionType.GlobalPlayers)]
    public void PlayerPressTutorialMark(OnPlayerPressTutorialMark ev)
    {
        Player player = ev.getPlayer();
        TutorialRoute? route = getTutorialRoute(player);
        QuestState? qs = getQuestState(player, false);
        if (route == null || qs == null)
            return;

        switch (ev.getMarkId())
        {
            case QUESTION_MARK_ID_1:
            {
                if (qs.isMemoState(1))
                {
                    showOnScreenMsg(player, NpcStringId.TALK_TO_NEWBIE_HELPER,
                        ExShowScreenMessagePacket.TOP_CENTER, 5000);

                    addRadar(player, route.NewbieHelperLocation.X, route.NewbieHelperLocation.Y,
                        route.NewbieHelperLocation.Z);
                    showTutorialHtml(player, "tutorial_04.html");
                    playTutorialVoice(player, "tutorial_voice_007");
                }

                break;
            }

            case QUESTION_MARK_ID_2:
            {
                if (qs.isMemoState(3))
                {
                    addRadar(player, route.NewbieHelperLocation.X, route.NewbieHelperLocation.Y,
                        route.NewbieHelperLocation.Z);
                    showTutorialHtml(player, "tutorial_06.html");
                }

                break;
            }
            case QUESTION_MARK_ID_3:
            {
                if (qs.isMemoState(5))
                {
                    addRadar(player, route.NewbieGuideLocation.X, route.NewbieGuideLocation.Y,
                        route.NewbieGuideLocation.Z);
                    playSound(player, "ItemSound.quest_tutorial");
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
        if (player.getLevel() > 6)
            return;

        if (getTutorialRoute(player) == null)
            return;

        QuestState? qs = getQuestState(player, true);
        if (qs != null && qs.getMemoState() < 2)
        {
            startQuestTimer("start_newbie_tutorial", TimeSpan.FromSeconds(5), null, player);
        }
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

    private sealed record TutorialRoute(
        int NewbieHelperId,
        int SupervisorId,
        Location NewbieHelperLocation,
        Location NewbieGuideLocation,
        string SupervisorName,
        string WelcomeHtml);
}
