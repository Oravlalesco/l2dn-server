using L2Dn.GameServer.Data.Sql;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.GameAssistant;
using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Scripts.Handlers.AdminCommandHandlers;

public sealed class AdminPremiumItem: IAdminCommandHandler
{
    private static readonly string[] Commands = ["admin_premium_item"];

    public bool useAdminCommand(string command, Player activeChar)
    {
        string[] args = command.Split(' ', 5, StringSplitOptions.RemoveEmptyEntries);
        if (args.Length is < 4 or > 5 || !int.TryParse(args[2], out int itemId) ||
            !long.TryParse(args[3], out long count))
        {
            BuilderUtil.sendSysMessage(activeChar,
                "Usage: //premium_item <character> <itemId> <count> [sender]");
            return false;
        }

        int characterId = CharInfoTable.getInstance().getIdByName(args[1]);
        string sender = args.Length == 5 ? args[4] : "Game Assistant";
        PremiumItemAssignmentResult result = PremiumItemService.getInstance()
            .Assign(characterId, itemId, count, sender);

        if (!result.Success)
        {
            BuilderUtil.sendSysMessage(activeChar, result.Message);
            return false;
        }

        BuilderUtil.sendSysMessage(activeChar,
            $"Premium item #{result.ItemNumber} assigned to {args[1]}: item {itemId} x{count}.");
        return true;
    }

    public string[] getAdminCommandList() => Commands;
}
