using L2Dn.GameServer.Db;
using L2Dn.GameServer.Enums;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Players;

namespace L2Dn.GameServer.Scripts.Handlers.DailyMissionHandlers;

/**
 * @author UnAfraid
 */
public class FishingDailyMissionHandler: AbstractDailyMissionHandler
{
	private readonly int _amount;
	private readonly int _minLevel;
	private readonly int _maxLevel;
	
	public FishingDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
	{
		_amount = holder.getRequiredCompletions();
		_minLevel = holder.getParams().getInt("minLevel", 0);
		_maxLevel = holder.getParams().getInt("maxLevel", int.MaxValue);
	}
	
	public override void init()
	{
		GlobalEvents.Global.Subscribe<OnPlayerFishing>(this, onPlayerFishing);
	}
	
	public override bool isAvailable(Player player)
	{
		return base.isAvailable(player);
	}
	
	private void onPlayerFishing(OnPlayerFishing @event)
	{
		Player player = @event.getPlayer();
		if (player.getLevel() < _minLevel || player.getLevel() > _maxLevel)
		{
			return;
		}
		
		if (@event.getReason() == FishingEndReason.WIN)
		{
			player.getDailyMissions().addProgress(getHolder());
		}
	}
}
