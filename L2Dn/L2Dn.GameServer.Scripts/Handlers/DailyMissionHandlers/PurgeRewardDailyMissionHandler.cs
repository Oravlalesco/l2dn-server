using L2Dn.GameServer.Db;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Items;

namespace L2Dn.GameServer.Scripts.Handlers.DailyMissionHandlers;

/**
 * @author CostyKiller
 */
public class PurgeRewardDailyMissionHandler: AbstractDailyMissionHandler
{
	private readonly int _amount;
	private readonly int _minLevel;
	private readonly int _maxLevel;
	
	public PurgeRewardDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
	{
		_amount = holder.getRequiredCompletions();
		_minLevel = holder.getParams().getInt("minLevel", 0);
		_maxLevel = holder.getParams().getInt("maxLevel", int.MaxValue);
	}
	
	public override void init()
	{
		GlobalEvents.Global.Subscribe<OnItemPurgeReward>(this, onItemPurgeReward);
	}
	
	public override bool isAvailable(Player player)
	{
		return base.isAvailable(player);
	}
	
	private void onItemPurgeReward(OnItemPurgeReward @event)
	{
		Player player = @event.getPlayer();
		if (player.getLevel() < _minLevel || player.getLevel() > _maxLevel)
		{
			return;
		}
		processPlayerProgress(player);
	}
	
	private void processPlayerProgress(Player player)
	{
		player.getDailyMissions().addProgress(getHolder());
	}
}
