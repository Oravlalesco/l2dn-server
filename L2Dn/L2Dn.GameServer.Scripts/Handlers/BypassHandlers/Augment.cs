using System.Globalization;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Network.OutgoingPackets.Variations;
using NLog;

namespace L2Dn.GameServer.Scripts.Handlers.BypassHandlers;

public class Augment: IBypassHandler
{
	private static readonly Logger _logger = LogManager.GetLogger(nameof(Augment));

	private const string CommandPrefix = "Augment";

	private static readonly string[] COMMANDS = [CommandPrefix];

	public bool useBypass(string command, Player player, Creature? target)
	{
		if (target is null || !target.isNpc())
		{
			return false;
		}

		if (!TryParseAugmentMode(command, out int mode))
		{
			_logger.Warn($"Invalid Augment bypass: [{command}]");
			return false;
		}

		switch (mode)
		{
			case 1:
			{
				player.sendPacket(ExShowVariationMakeWindowPacket.STATIC_PACKET);
				return true;
			}
			case 2:
			{
				player.sendPacket(ExShowVariationCancelWindowPacket.STATIC_PACKET);
				return true;
			}
			default:
			{
				_logger.Warn($"Unknown Augment mode {mode} in bypass: [{command}]");
				return false;
			}
		}
	}

	private static bool TryParseAugmentMode(string command, out int mode)
	{
		mode = 0;
		if (string.IsNullOrWhiteSpace(command) ||
		    !command.AsSpan().TrimStart().StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase))
		{
			return false;
		}

		ReadOnlySpan<char> args = command.AsSpan(CommandPrefix.Length).TrimStart();
		return int.TryParse(args, NumberStyles.Integer, CultureInfo.InvariantCulture, out mode);
	}

	public string[] getBypassList()
	{
		return COMMANDS;
	}
}
