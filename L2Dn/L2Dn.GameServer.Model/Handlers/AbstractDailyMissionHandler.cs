using System.Runtime.CompilerServices;
using L2Dn.Extensions;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Dto;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Holders;
using L2Dn.GameServer.Network.Enums;
using L2Dn.GameServer.Network.OutgoingPackets;
using Microsoft.EntityFrameworkCore;
using NLog;
using Clan = L2Dn.GameServer.Model.Clans.Clan;

namespace L2Dn.GameServer.Handlers;

/**
 * @author Sdw
 */
public abstract class AbstractDailyMissionHandler: IDisposable
{
	public const int MISSION_LEVEL_POINTS = 97224;
	public const int CLAN_EXP = 94481;

	protected readonly Logger LOGGER = LogManager.GetLogger(nameof(AbstractDailyMissionHandler));

	private readonly DailyMissionDataHolder _holder;

	protected AbstractDailyMissionHandler(DailyMissionDataHolder holder)
	{
		_holder = holder;
	}

	public DailyMissionDataHolder getHolder()
	{
		return _holder;
	}

	public virtual bool isAvailable(Player player)
	{
		return player.getDailyMissions().getStatus(_holder.getId()) == DailyMissionStatus.AVAILABLE;
	}

	public abstract void init();

	public virtual void refresh(Player player)
	{
		player.getDailyMissions().updateEntry(_holder, entry =>
		{
			if (entry.getStatus() == DailyMissionStatus.NOT_AVAILABLE &&
				entry.getProgress() >= _holder.getRequiredCompletions())
			{
				entry.setStatus(DailyMissionStatus.AVAILABLE);
			}
		}, synchronizeOnStatus: false);
	}

	public virtual int getProgress(Player player)
	{
		return player.getDailyMissions().getProgress(_holder.getId());
	}

	[MethodImpl(MethodImplOptions.Synchronized)]
	public virtual void reset()
	{
		World.getInstance().getPlayers().ForEach(player => player.getDailyMissions().resetExpired(DateTimeOffset.Now));
	}

	public bool requestReward(Player player)
	{
		return isAvailable(player) && player.getDailyMissions().claim(_holder);
	}

	protected void giveRewards(Player player)
	{
		foreach (ItemHolder holder in _holder.getRewards())
		{
			switch (holder.getId())
			{
				case CLAN_EXP:
				{
					Clan? clan = player.getClan();
					if (clan != null)
					{
						int expAmount = (int) holder.getCount();
						clan.addExp(player.ObjectId, expAmount);
						SystemMessagePacket packet = new SystemMessagePacket(SystemMessageId.YOU_HAVE_OBTAINED_S1_X_S2);
						packet.Params.addItemName(MISSION_LEVEL_POINTS).addLong(expAmount);
						player.sendPacket(packet);
					}
					break;
				}
				case MISSION_LEVEL_POINTS:
				{
					int levelPoints = (int) holder.getCount();
					MissionLevelPlayerDataHolder info = player.getMissionLevelProgress();
					info.calculateEXP(levelPoints);
					info.storeInfoInVariable(player);
					SystemMessagePacket packet = new SystemMessagePacket(SystemMessageId.YOU_HAVE_OBTAINED_S1_X_S2);
					packet.Params.addItemName(MISSION_LEVEL_POINTS).addLong(levelPoints);
					player.sendPacket(packet);
					break;
				}
				default:
				{
					player.addItem("One Day Reward", holder, player, true);
					break;
				}
			}
		}
	}

	public virtual void Dispose()
	{
		GlobalEvents.Global.UnsubscribeAllTypes(this);
		GlobalEvents.Npcs.UnsubscribeAllTypes(this);
		GlobalEvents.Monsters.UnsubscribeAllTypes(this);
		GlobalEvents.Players.UnsubscribeAllTypes(this);
		GC.SuppressFinalize(this);
	}
}
