using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Html;
using L2Dn.GameServer.Model.ItemContainers;
using L2Dn.GameServer.Network.OutgoingPackets;

namespace L2Dn.GameServer.Model.GameAssistant;

public static class GameAssistantUi
{
    private const string HtmlRoot = "scripts/ai/others/GameAssistant/";

    public static void ShowMain(Player player, Npc? npc = null) =>
        Show(player, npc, npc == null ? "32478-button.html" : "32478.html");

    public static void ShowEvents(Player player, Npc? npc = null) =>
        Show(player, npc, npc == null ? "events-button.html" : "events.html");

    public static void ShowError(Player player, string message)
    {
        player.sendMessage(message);
        player.sendPacket(new ExShowScreenMessagePacket(message, ExShowScreenMessagePacket.TOP_CENTER, 5000));
    }

    private static void Show(Player player, Npc? npc, string fileName)
    {
        player.setMultiSell(null);
        if (player.getActiveWarehouse() is PlayerFreight)
            player.setActiveWarehouse(null);

        HtmlContent html = HtmlContent.LoadFromFile(HtmlRoot + fileName, player);
        if (npc == null)
        {
            player.sendPacket(new ExPremiumManagerShowHtmlPacket(html));
            return;
        }

        html.Replace("%objectId%", npc.ObjectId);
        player.sendPacket(new NpcHtmlMessagePacket(npc.ObjectId, 0, html));
        player.sendPacket(ActionFailedPacket.STATIC_PACKET);
    }
}
