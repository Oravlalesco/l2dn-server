using L2Dn.GameServer.Db;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Players;

namespace L2Dn.GameServer.Scripts.Handlers.DailyMissionHandlers;

/**
 * @author Sdw
 */
public class LevelDailyMissionHandler: AbstractDailyMissionHandler
{
	private readonly int _level;
	private readonly bool _dualclass;
	
	public LevelDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
	{
		_level = holder.getParams().getInt("level");
		_dualclass = holder.getParams().getBoolean("dualclass", false);
	}
	
	public override void init()
	{
		GlobalEvents.Players.Subscribe<OnPlayerLevelChanged>(this, onPlayerLevelChanged);
	}
	
	public override bool isAvailable(Player player)
	{
		if (player.getLevel() >= _level && player.isDualClassActive() == _dualclass)
		{
			player.getDailyMissions().makeAvailable(getHolder(), false);
		}

		return base.isAvailable(player);
	}

	public override void refresh(Player player)
	{
		base.refresh(player);
		if (player.getLevel() >= _level && player.isDualClassActive() == _dualclass)
		{
			player.getDailyMissions().makeAvailable(getHolder(), false);
		}
	}
	
	public override void reset()
	{
		// Level rewards doesn't reset daily
	}
	
	public override int getProgress(Player player)
	{
		return _level;
	}
	
	private void onPlayerLevelChanged(OnPlayerLevelChanged @event)
	{
		Player player = @event.getPlayer();
		if (player.getLevel() >= _level && player.isDualClassActive() == _dualclass)
		{
			player.getDailyMissions().makeAvailable(getHolder());
		}
	}
}
