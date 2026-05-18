using System.Globalization;
using L2Dn.GameServer.Data;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Html;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using L2Dn.GameServer.TaskManagers;
using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Scripts.Handlers.AdminCommandHandlers;

/**
 * This class handles following admin commands: - server_shutdown [sec] = shows menu or shuts down server in sec seconds
 */
public class AdminShutdown: IAdminCommandHandler
{
	private static readonly string[] ADMIN_COMMANDS =
    [
        "admin_server_shutdown",
		"admin_server_restart",
		"admin_server_abort",
    ];

	public bool useAdminCommand(string command, Player activeChar)
	{
		const string shutdownPrefix = "admin_server_shutdown";
		const string restartPrefix = "admin_server_restart";

		if (command.startsWith(shutdownPrefix))
		{
			string val = command.Length > shutdownPrefix.Length
				? command.Substring(shutdownPrefix.Length).Trim()
				: string.Empty;
			if (int.TryParse(val, CultureInfo.InvariantCulture, out int valInt))
			{
				serverShutdown(activeChar, valInt, false);
			}
			else
			{
				BuilderUtil.sendSysMessage(activeChar, "Usage: //server_shutdown <seconds>");
				sendHtmlForm(activeChar);
			}
		}
		else if (command.startsWith(restartPrefix))
		{
			string val = command.Length > restartPrefix.Length
				? command.Substring(restartPrefix.Length).Trim()
				: string.Empty;
			if (int.TryParse(val, CultureInfo.InvariantCulture, out int valInt))
			{
				serverShutdown(activeChar, valInt, true);
			}
			else
			{
				BuilderUtil.sendSysMessage(activeChar, "Usage: //server_restart <seconds>");
				sendHtmlForm(activeChar);
			}
		}
		else if (command.startsWith("admin_server_abort"))
		{
			serverAbort(activeChar);
		}
		return true;
	}

	public string[] getAdminCommandList()
	{
		return ADMIN_COMMANDS;
	}

	private void sendHtmlForm(Player activeChar)
	{
		HtmlContent htmlContent = HtmlContent.LoadFromFile("html/admin/shutdown.htm", activeChar);
		int t = GameTimeTaskManager.getInstance().getGameTime();
		int h = t / 60;
		int m = t % 60;

		htmlContent.Replace("%count%", World.getInstance().getPlayers().Count.ToString());
		htmlContent.Replace("%used%", GC.GetTotalMemory(false).ToString());
		htmlContent.Replace("%time%", h + ":" + m);
		NpcHtmlMessagePacket adminReply = new NpcHtmlMessagePacket(null, 1, htmlContent);
		activeChar.sendPacket(adminReply);
	}

	private void serverShutdown(Player activeChar, int seconds, bool restart)
	{
		Shutdown.getInstance().startShutdown(activeChar, seconds, restart);
	}

	private void serverAbort(Player activeChar)
	{
		Shutdown.getInstance().abort(activeChar);
	}
}