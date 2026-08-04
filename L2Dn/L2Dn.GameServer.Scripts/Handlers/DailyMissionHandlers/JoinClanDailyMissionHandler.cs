using L2Dn.GameServer.Db;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Clans;

namespace L2Dn.GameServer.Scripts.Handlers.DailyMissionHandlers;

/**
 * @author kamikadzz
 */
public class JoinClanDailyMissionHandler: AbstractDailyMissionHandler
{
	public JoinClanDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
	{
	}

	public override bool isAvailable(Player player)
	{
		return base.isAvailable(player);
	}

	public override void init()
	{
		GlobalEvents.Global.Subscribe<OnClanJoin>(this, onPlayerClanJoin);
		GlobalEvents.Global.Subscribe<OnClanCreate>(this, onPlayerClanCreate);
	}

	private void onPlayerClanJoin(OnClanJoin @event)
	{
		Player? player = @event.getClanMember().getPlayer(); // TODO: refactor OnClanJoin, it must contain Player and Clan fields
        if (player == null)
            return;

		processMission(player);
	}

	private void onPlayerClanCreate(OnClanCreate @event)
	{
		Player player = @event.getPlayer();
		processMission(player);
	}

	private void processMission(Player player)
	{
		player.getDailyMissions().makeAvailable(getHolder());
	}
}
