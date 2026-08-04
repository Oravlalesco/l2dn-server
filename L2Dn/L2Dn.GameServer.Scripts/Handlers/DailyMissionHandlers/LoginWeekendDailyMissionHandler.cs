using L2Dn.GameServer.Db;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Players;
using L2Dn.GameServer.Model.DailyMissions;

namespace L2Dn.GameServer.Scripts.Handlers.DailyMissionHandlers;

/**
 * @author Iris, Mobius
 */
public class LoginWeekendDailyMissionHandler: AbstractDailyMissionHandler
{
	public LoginWeekendDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
	{
	}
	
	public override bool isAvailable(Player player)
	{
		return base.isAvailable(player);
	}
	
	public override void init()
	{
		GlobalEvents.Global.Subscribe<OnPlayerLogin>(this, onPlayerLogin);
	}

	public override void refresh(Player player)
	{
		base.refresh(player);
		if (DailyMissionCycle.isWeekendWindow(DateTimeOffset.Now))
		{
			player.getDailyMissions().makeAvailable(getHolder(), false);
		}
	}
	
	private void onPlayerLogin(OnPlayerLogin @event)
	{
		Player player = @event.getPlayer();
		if (DailyMissionCycle.isWeekendWindow(DateTimeOffset.Now))
		{
			player.getDailyMissions().makeAvailable(getHolder());
		}
	}
}
