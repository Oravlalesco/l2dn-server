using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.GameAssistant;
using L2Dn.GameServer.Model.ItemContainers;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using Config = L2Dn.GameServer.Configuration.Config;

namespace L2Dn.GameServer.Scripts.Handlers.BypassHandlers;

public sealed class GameAssistant: IBypassHandler
{
    private const int GameAssistantNpcId = 32478;
    private static readonly string[] Commands = ["game_assistant"];

    public bool useBypass(string command, Player player, Creature? target)
    {
        if (!Config.GameAssistant.GAME_ASSISTANT_ENABLED)
        {
            player.setMultiSell(null);
            GameAssistantUi.ShowError(player, "Game Assistant is temporarily unavailable.");
            return false;
        }

        Npc? npc = target as Npc;
        if (target != null && (npc == null || npc.getId() != GameAssistantNpcId))
        {
            GameAssistantUi.ShowError(player, "This Game Assistant action is not available from the selected target.");
            return false;
        }

        // Navigating away from freight withdrawal invalidates that window's server-side container.
        if (player.getActiveWarehouse() is PlayerFreight)
            player.setActiveWarehouse(null);

        player.setMultiSell(null);

        string action = command.Length > Commands[0].Length
            ? command[Commands[0].Length..].Trim()
            : "home";

        if (action.Equals("home", StringComparison.OrdinalIgnoreCase))
        {
            GameAssistantUi.ShowMain(player, npc);
            return true;
        }

        if (action.Equals("events", StringComparison.OrdinalIgnoreCase))
        {
            GameAssistantUi.ShowEvents(player, npc);
            return true;
        }

        if (action.Equals("premium", StringComparison.OrdinalIgnoreCase))
        {
            if (player.getPremiumItemList().Count == 0)
            {
                player.sendPacket(SystemMessageId.THERE_ARE_NO_MORE_DIMENSIONAL_ITEMS_TO_BE_FOUND);
                player.sendPacket(new ExShowScreenMessagePacket("You have no purchased items waiting to be claimed.",
                    ExShowScreenMessagePacket.TOP_CENTER, 5000));
            }
            else
            {
                player.sendPacket(new ExGetPremiumItemListPacket(player));
            }

            return true;
        }

        if (action.StartsWith("exchange ", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(action[9..].Trim(), out int listId))
        {
            return ItemMultisellData.getInstance().TryOpen(player, listId);
        }

        GameAssistantUi.ShowError(player, "This Game Assistant action is not available.");
        return false;
    }

    public string[] getBypassList() => Commands;
}
