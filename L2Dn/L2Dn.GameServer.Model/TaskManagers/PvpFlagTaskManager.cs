using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Utilities;
using NLog;
using ThreadPool = L2Dn.GameServer.Utilities.ThreadPool;

namespace L2Dn.GameServer.TaskManagers;

/**
 * @author Mobius
 */
public class PvpFlagTaskManager: Runnable
{
	private static readonly Logger LOGGER = LogManager.GetLogger(nameof(PvpFlagTaskManager));
	private static readonly Set<Player> PLAYERS = new();
	private static int _working;
	
	protected PvpFlagTaskManager()
	{
		ThreadPool.scheduleAtFixedRate(this, 1000, 1000); // TODO: high priority task
	}
	
	public void run()
	{
		if (Interlocked.Exchange(ref _working, 1) != 0)
		{
			return;
		}

		try
		{
			if (PLAYERS.isEmpty())
			{
				return;
			}

			DateTime currentTime = DateTime.UtcNow;
			foreach (Player player in PLAYERS)
			{
				try
				{
					PvpFlagStatus status = PvpFlagStateMachine.Evaluate(currentTime, player.getPvpFlagLasts());
					if (status == PvpFlagStatus.None)
					{
						player.stopPvPFlag();
					}
					else
					{
						player.updatePvPFlag(status);
					}
				}
				catch (Exception exception)
				{
					LOGGER.Error(exception, $"Failed to update PvP flag for player {player.ObjectId}.");
				}
			}
		}
		finally
		{
			Volatile.Write(ref _working, 0);
		}
	}
	
	public void add(Player player)
	{
		PLAYERS.add(player);
	}
	
	public void remove(Player player)
	{
		PLAYERS.remove(player);
	}
	
	public static PvpFlagTaskManager getInstance()
	{
		return SingletonHolder.INSTANCE;
	}
	
	private static class SingletonHolder
	{
		public static readonly PvpFlagTaskManager INSTANCE = new PvpFlagTaskManager();
	}
}
