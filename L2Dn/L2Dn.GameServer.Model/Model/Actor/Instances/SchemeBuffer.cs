using System.Globalization;
using System.Text;
using L2Dn.Extensions;
using L2Dn.GameServer.Data;
using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Model.Actor.Templates;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Model.Html;
using L2Dn.GameServer.Model.Skills;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.Utilities;
using NLog;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Model.Actor.Instances;

public class SchemeBuffer: Npc
{
    private const int PAGE_LIMIT = 6;

    public SchemeBuffer(NpcTemplate template): base(template)
    {
    }

    public override void onBypassFeedback(Player player, string commandValue)
    {
        if (!SchemeBufferBypass.TryParse(commandValue, out SchemeBufferBypass bypass))
        {
            player.sendMessage("Comando del buffer incorrecto.");
            return;
        }

        switch (bypass.Command)
        {
            case var cmd when cmd.startsWith("menu"):
                handleMenu(player);
                break;
            case var cmd when cmd.startsWith("cleanup"):
                handleCleanup(player);
                break;
            case var cmd when cmd.startsWith("heal"):
                handleHeal(player);
                break;
            case var cmd when cmd.startsWith("support"):
                PlayerBufferSchemeContext.ClearActiveScheme(player.ObjectId);
                showGiveBuffsWindow(player);
                break;
            case var cmd when cmd.startsWith("givebuffs"):
                handleGiveBuffs(player, bypass);
                break;
            case var cmd when cmd.startsWith("editschemes"):
                handleEditSchemes(player, bypass);
                break;
            case "skillselect":
                handleSkillSelect(player, bypass);
                break;
            case "skillunselect":
                handleSkillUnselect(player, bypass);
                break;
            case var cmd when cmd.startsWith("createscheme"):
                handleCreateScheme(player, bypass);
                break;
            case var cmd when cmd.startsWith("deletescheme"):
                handleDeleteScheme(player, bypass);
                break;
        }
    }

    private void handleMenu(Player player)
    {
        sendMainHtml(player);
    }

    private void handleCleanup(Player player)
    {
        player.stopAllEffects();

        Summon? summon = player.getPet();
        if (summon != null)
            summon.stopAllEffects();

        player.getServitors().Values.ForEach(servitor => servitor.stopAllEffects());
        sendMainHtml(player);
    }

    private void handleHeal(Player player)
    {
        player.setCurrentHpMp(player.getMaxHp(), player.getMaxMp());
        player.setCurrentCp(player.getMaxCp());

        Summon? summon = player.getPet();
        if (summon != null)
            summon.setCurrentHpMp(summon.getMaxHp(), summon.getMaxMp());

        player.getServitors().Values.
            ForEach(servitor => servitor.setCurrentHpMp(servitor.getMaxHp(), servitor.getMaxMp()));
        sendMainHtml(player);
    }

    private void handleGiveBuffs(Player player, SchemeBufferBypass bypass)
    {
        string parsedScheme = bypass.TokenAt(1);
        if (!bypass.TryParseInt(2, out int cost) || !tryResolveScheme(player, parsedScheme, out string schemeName))
        {
            player.sendMessage("Esquema desconocido o no valido.");
            showGiveBuffsWindow(player);
            return;
        }

        bool buffSummons = bypass.Tokens.Count > 3 && bypass.TokenAt(3).equalsIgnoreCase("pet");
        if (buffSummons && player.getPet() == null && !player.hasServitors())
        {
            player.sendMessage("You don't have a pet.");
            return;
        }

        if (cost == 0 || (Config.SchemeBuffer.BUFFER_ITEM_ID == 57 && player.reduceAdena("NPC Buffer", cost, this, true)) ||
            (Config.SchemeBuffer.BUFFER_ITEM_ID != 57 &&
             player.destroyItemByItemId("NPC Buffer", Config.SchemeBuffer.BUFFER_ITEM_ID, cost, player, true)))
        {
            List<Skill> buffSkills = [];
            List<Skill> danceSkills = [];
            foreach (int skillId in SchemeBufferTable.getInstance().getScheme(player.ObjectId, schemeName))
            {
                BuffSkillHolder? availableBuff = SchemeBufferTable.getInstance().getAvailableBuff(skillId);
                if (availableBuff == null)
                    continue;

                Skill? skill = SkillData.getInstance().getSkill(skillId, availableBuff.getLevel());
                if (skill == null)
                    continue;

                if (skill.isDance())
                    danceSkills.Add(skill);
                else
                    buffSkills.Add(skill);
            }

            void applyScheme(Creature target)
            {
                foreach (Skill skill in buffSkills)
                    skill.applyEffects(player, target);
                foreach (Skill skill in danceSkills)
                    skill.applyEffects(player, target);
            }

            if (buffSummons)
            {
                Pet? pet = player.getPet();
                if (pet != null)
                    applyScheme(pet);

                player.getServitors().Values.ForEach(applyScheme);
            }
            else
            {
                applyScheme(player);
            }
        }
    }

    private void handleEditSchemes(Player player, SchemeBufferBypass bypass)
    {
        string groupType = bypass.TokenAt(1);
        string parsedScheme = bypass.TokenAt(2);
        if (!bypass.TryParseInt(3, out int page))
            page = 1;

        if (!tryResolveScheme(player, parsedScheme, out string schemeName))
        {
            logInvalidScheme(player, parsedScheme);
            player.sendMessage("Esquema no valido. Vuelve a Magic support y pulsa Edit de nuevo.");
            showGiveBuffsWindow(player);
            return;
        }

        PlayerBufferSchemeContext.SetActiveScheme(player.ObjectId, schemeName);
        showEditSchemeWindow(player, groupType, schemeName, page);
    }

    private void handleSkillSelect(Player player, SchemeBufferBypass bypass)
    {
        if (!tryParseSkillBypass(player, bypass, out string groupType, out string schemeName, out int skillId, out int page))
            return;

        List<int> skills = SchemeBufferTable.getInstance().getScheme(player.ObjectId, schemeName);
        bool schemeChanged = false;
        Skill? skill = SkillData.getInstance().getSkill(skillId, SkillData.getInstance().getMaxLevel(skillId));
        if (skill != null)
        {
            if (skill.isDance())
            {
                if (getCountOf(skills, true) < Config.Character.DANCES_MAX_AMOUNT)
                {
                    skills.Add(skillId);
                    schemeChanged = true;
                }
                else
                {
                    player.sendMessage("This scheme has reached the maximum amount of dances/songs.");
                }
            }
            else
            {
                if (getCountOf(skills, false) < player.getStat().getMaxBuffCount())
                {
                    skills.Add(skillId);
                    schemeChanged = true;
                }
                else
                {
                    player.sendMessage("This scheme has reached the maximum amount of buffs.");
                }
            }
        }

        if (schemeChanged)
            SchemeBufferTable.getInstance().persistScheme(player.ObjectId, schemeName);

        showEditSchemeWindow(player, groupType, schemeName, page);
    }

    private void handleSkillUnselect(Player player, SchemeBufferBypass bypass)
    {
        if (!tryParseSkillBypass(player, bypass, out string groupType, out string schemeName, out int skillId, out int page))
            return;

        List<int> skills = SchemeBufferTable.getInstance().getScheme(player.ObjectId, schemeName);
        if (skills.Remove(skillId))
            SchemeBufferTable.getInstance().persistScheme(player.ObjectId, schemeName);

        showEditSchemeWindow(player, groupType, schemeName, page);
    }

    private bool tryParseSkillBypass(Player player, SchemeBufferBypass bypass, out string groupType,
        out string schemeName, out int skillId, out int page)
    {
        groupType = bypass.TokenAt(1);
        string parsedScheme = bypass.TokenAt(2);
        schemeName = string.Empty;
        skillId = 0;
        page = 1;

        if (!bypass.TryParseInt(3, out skillId) || !bypass.TryParseInt(4, out page))
        {
            player.sendMessage("Comando del buffer incorrecto.");
            showGiveBuffsWindow(player);
            return false;
        }

        if (!tryResolveScheme(player, parsedScheme, out schemeName))
        {
            logInvalidScheme(player, parsedScheme);
            player.sendMessage("Esquema no valido. Vuelve a Magic support y pulsa Edit de nuevo.");
            showGiveBuffsWindow(player);
            return false;
        }

        PlayerBufferSchemeContext.SetActiveScheme(player.ObjectId, schemeName);
        return true;
    }

    private void handleCreateScheme(Player player, SchemeBufferBypass bypass)
    {
        try
        {
            string schemeName = bypass.TokenAt(1);
            if (!SchemeBufferTable.getInstance().tryAddScheme(player.ObjectId, schemeName, out string? error))
            {
                player.sendMessage(error ?? "No se pudo crear el esquema.");
                return;
            }

            string resolved = SchemeBufferSchemeNames.Normalize(schemeName)!;
            PlayerBufferSchemeContext.SetActiveScheme(player.ObjectId, resolved);
            showGiveBuffsWindow(player);
        }
        catch (Exception e)
        {
            LOGGER.Error(e);
            player.sendMessage("Scheme's name must contain up to 14 chars.");
        }
    }

    private void handleDeleteScheme(Player player, SchemeBufferBypass bypass)
    {
        try
        {
            if (!SchemeBufferTable.getInstance().tryRemoveScheme(player.ObjectId, bypass.TokenAt(1), out string? error))
                player.sendMessage(error ?? "This scheme name is invalid.");
        }
        catch (Exception e)
        {
            LOGGER.Error(e);
            player.sendMessage("This scheme name is invalid.");
        }

        showGiveBuffsWindow(player);
    }

    private void sendMainHtml(Player player)
    {
        PlayerBufferSchemeContext.ClearActiveScheme(player.ObjectId);
        HtmlContent htmlContent = HtmlContent.LoadFromFile(getHtmlPath(getId(), 0, player), player);
        htmlContent.Replace("%objectId%", ObjectId.ToString());
        player.sendPacket(new NpcHtmlMessagePacket(ObjectId, 0, htmlContent));
    }

    public override string getHtmlPath(int npcId, int value, Player? player)
    {
        string filename = value == 0
            ? npcId.ToString(CultureInfo.InvariantCulture)
            : npcId + "-" + value;

        return "html/mods/SchemeBuffer/" + filename + ".htm";
    }

    private void showGiveBuffsWindow(Player player)
    {
        StringBuilder sb = new StringBuilder(200);
        Map<string, List<int>>? schemes = SchemeBufferTable.getInstance().getPlayerSchemes(player.ObjectId);
        if (schemes == null || schemes.Count == 0)
        {
            sb.Append("<font color=\"LEVEL\">You haven't defined any scheme.</font>");
        }
        else
        {
            foreach (var scheme in schemes)
            {
                if (!SchemeBufferSchemeNames.IsValidFormat(scheme.Key, out _))
                    continue;

                int cost = getFee(scheme.Value);
                sb.Append("<font color=\"LEVEL\">" + scheme.Key + " [" + scheme.Value.Count + " skill(s)]" +
                    (cost > 0 ? " - cost: " + cost : "") + "</font><br1>");

                sb.Append("<a action=\"bypass npc_%objectId%_givebuffs;" + scheme.Key + ";" + cost +
                    "\">Use on Me</a>&nbsp;|&nbsp;");

                sb.Append("<a action=\"bypass npc_%objectId%_givebuffs;" + scheme.Key + ";" + cost +
                    ";pet\">Use on Pet</a>&nbsp;|&nbsp;");

                sb.Append("<a action=\"bypass npc_%objectId%_editschemes;Buffs;" + scheme.Key +
                    ";1\">Edit</a>&nbsp;|&nbsp;");

                sb.Append("<a action=\"bypass npc_%objectId%_deletescheme;" + scheme.Key + "\">Delete</a><br>");
            }
        }

        HtmlContent htmlContent = HtmlContent.LoadFromFile(getHtmlPath(getId(), 1, player), player);
        htmlContent.Replace("%schemes%", sb.ToString());
        htmlContent.Replace("%max_schemes%", Config.SchemeBuffer.BUFFER_MAX_SCHEMES.ToString());
        htmlContent.Replace("%objectId%", ObjectId.ToString());
        player.sendPacket(new NpcHtmlMessagePacket(ObjectId, 0, htmlContent));
    }

    private void showEditSchemeWindow(Player player, string groupType, string schemeName, int page)
    {
        List<int> schemeSkills = SchemeBufferTable.getInstance().getScheme(player.ObjectId, schemeName);

        HtmlContent htmlContent = HtmlContent.LoadFromFile(getHtmlPath(getId(), 2, player), player);
        htmlContent.Replace("%schemename%", schemeName);
        htmlContent.Replace("%count%",
            getCountOf(schemeSkills, false) + " / " + player.getStat().getMaxBuffCount() + " buffs, " +
            getCountOf(schemeSkills, true) + " / " + Config.Character.DANCES_MAX_AMOUNT + " dances/songs");

        htmlContent.Replace("%typesframe%", getTypesFrame(groupType, schemeName));
        htmlContent.Replace("%skilllistframe%", getGroupSkillList(player, groupType, schemeName, page));
        htmlContent.Replace("%objectId%", ObjectId.ToString());
        player.sendPacket(new NpcHtmlMessagePacket(ObjectId, 0, htmlContent));
    }

    private string getGroupSkillList(Player player, string groupType, string schemeName, int pageValue)
    {
        List<int> skills = SchemeBufferTable.getInstance().getSkillsIdsByType(groupType);
        if (skills.Count == 0)
            return "That group doesn't contain any skills.";

        int max = Math.Max(1, MathUtil.countPagesNumber(skills.Count, PAGE_LIMIT));
        int page = pageValue;
        if (page < 1)
            page = 1;

        if (page > max)
            page = max;

        int startIndex = (page - 1) * PAGE_LIMIT;
        int sliceCount = Math.Min(PAGE_LIMIT, skills.Count - startIndex);
        if (sliceCount <= 0)
            return string.Empty;

        skills = skills.GetRange(startIndex, sliceCount);

        List<int> schemeSkills = SchemeBufferTable.getInstance().getScheme(player.ObjectId, schemeName);
        StringBuilder sb = new StringBuilder(skills.Count * 150);
        int row = 0;
        foreach (int skillId in skills)
        {
            Skill? skill = SkillData.getInstance().getSkill(skillId, 1);
            if (skill == null)
                continue;

            BuffSkillHolder? availableBuff = SchemeBufferTable.getInstance().getAvailableBuff(skillId);
            if (availableBuff == null)
                continue;

            sb.Append(row % 2 == 0 ? "<table width=\"280\" bgcolor=\"000000\"><tr>" : "<table width=\"280\"><tr>");

            if (schemeSkills.Contains(skillId))
            {
                sb.Append("<td height=40 width=40><img src=\"" + skill.getIcon() +
                    "\" width=32 height=32></td><td width=190>" + skill.getName() +
                    "<br1><font color=\"B09878\">" +
                    availableBuff.getDescription() +
                    "</font></td><td><button value=\" \" action=\"bypass npc_%objectId%_skillunselect;" +
                    groupType + ";" + schemeName + ";" + skillId + ";" + page +
                    "\" width=32 height=32 back=\"L2UI_CH3.mapbutton_zoomout2\" fore=\"L2UI_CH3.mapbutton_zoomout1\"></td>");
            }
            else
            {
                sb.Append("<td height=40 width=40><img src=\"" + skill.getIcon() +
                    "\" width=32 height=32></td><td width=190>" + skill.getName() +
                    "<br1><font color=\"B09878\">" +
                    availableBuff.getDescription() +
                    "</font></td><td><button value=\" \" action=\"bypass npc_%objectId%_skillselect;" +
                    groupType + ";" + schemeName + ";" + skillId + ";" + page +
                    "\" width=32 height=32 back=\"L2UI_CH3.mapbutton_zoomin2\" fore=\"L2UI_CH3.mapbutton_zoomin1\"></td>");
            }

            sb.Append("</tr></table><img src=\"L2UI.SquareGray\" width=277 height=1>");
            row++;
        }

        sb.Append("<br><img src=\"L2UI.SquareGray\" width=277 height=1><table width=\"100%\" bgcolor=000000><tr>");
        if (page > 1)
        {
            sb.Append("<td align=left width=70><a action=\"bypass npc_" + ObjectId + "_editschemes;" +
                groupType + ";" + schemeName + ";" + (page - 1) + "\">Previous</a></td>");
        }
        else
        {
            sb.Append("<td align=left width=70>Previous</td>");
        }

        sb.Append("<td align=center width=100>Page " + page + "</td>");
        if (page < max)
        {
            sb.Append("<td align=right width=70><a action=\"bypass npc_" + ObjectId + "_editschemes;" +
                groupType + ";" + schemeName + ";" + (page + 1) + "\">Next</a></td>");
        }
        else
        {
            sb.Append("<td align=right width=70>Next</td>");
        }

        sb.Append("</tr></table><img src=\"L2UI.SquareGray\" width=277 height=1>");
        return sb.ToString();
    }

    private static string getTypesFrame(string groupType, string schemeName)
    {
        StringBuilder sb = new StringBuilder(500);
        sb.Append("<table>");

        int count = 0;
        foreach (string type in SchemeBufferTable.getInstance().getSkillTypes())
        {
            if (count == 0)
                sb.Append("<tr>");

            if (groupType.equalsIgnoreCase(type))
            {
                sb.Append("<td width=65>" + type + "</td>");
            }
            else
            {
                sb.Append("<td width=65><a action=\"bypass npc_%objectId%_editschemes;" + type + ";" + schemeName +
                    ";1\">" + type + "</a></td>");
            }

            count++;
            if (count == 4)
            {
                sb.Append("</tr>");
                count = 0;
            }
        }

        if (!sb.ToString().EndsWith("</tr>"))
            sb.Append("</tr>");

        sb.Append("</table>");
        return sb.ToString();
    }

    private static int getFee(List<int> list)
    {
        if (Config.SchemeBuffer.BUFFER_STATIC_BUFF_COST > 0)
            return list.Count * Config.SchemeBuffer.BUFFER_STATIC_BUFF_COST;

        int fee = 0;
        foreach (int sk in list)
        {
            BuffSkillHolder? availableBuff = SchemeBufferTable.getInstance().getAvailableBuff(sk);
            if (availableBuff != null)
                fee += availableBuff.getPrice();
        }

        return fee;
    }

    private static int getCountOf(List<int> skills, bool dances)
    {
        int count = 0;
        foreach (int skillId in skills)
        {
            Skill? skill = SkillData.getInstance().getSkill(skillId, 1);
            if (skill != null && skill.isDance() == dances)
                count++;
        }

        return count;
    }

    private static bool tryResolveScheme(Player player, string parsedScheme, out string schemeName)
    {
        schemeName = string.Empty;
        SchemeBufferTable table = SchemeBufferTable.getInstance();
        string? active = PlayerBufferSchemeContext.GetActiveScheme(player.ObjectId);
        if (!table.tryResolveSchemeKey(player.ObjectId, parsedScheme, active, out string resolved))
            return false;

        schemeName = resolved;
        return true;
    }

    private static void logInvalidScheme(Player player, string parsedScheme)
    {
        Map<string, List<int>>? schemes = SchemeBufferTable.getInstance().getPlayerSchemes(player.ObjectId);
        string known = schemes == null
            ? string.Empty
            : string.Join(", ", schemes.Keys.Select(k => "'" + k + "'"));
        string? active = PlayerBufferSchemeContext.GetActiveScheme(player.ObjectId);
        LogManager.GetLogger(nameof(SchemeBuffer)).Warn(
            "SchemeBuffer: invalid scheme for player {0}: parsed='{1}', active='{2}', known=[{3}]",
            player.ObjectId, parsedScheme, active ?? string.Empty, known);
    }
}
