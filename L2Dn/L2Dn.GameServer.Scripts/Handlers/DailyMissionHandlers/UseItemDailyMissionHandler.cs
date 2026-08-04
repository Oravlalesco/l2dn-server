using L2Dn.GameServer.Db;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Items;
using L2Dn.GameServer.Utilities;

namespace L2Dn.GameServer.Scripts.Handlers.DailyMissionHandlers;

/**
 * @author CostyKiller
 */
public class UseItemDailyMissionHandler: AbstractDailyMissionHandler
{
	private readonly int _amount;
	private readonly int _minLevel;
	private readonly int _maxLevel;
	private readonly Set<int> _itemIds = new();
	
	public UseItemDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
	{
		_amount = holder.getRequiredCompletions();
		_minLevel = holder.getParams().getInt("minLevel", 0);
		_maxLevel = holder.getParams().getInt("maxLevel", int.MaxValue);
		string itemIds = holder.getParams().getString("itemIds", "");
		if (!string.IsNullOrEmpty(itemIds))
		{
			foreach (string s in itemIds.Split(','))
			{
				int id = int.Parse(s.Trim());
				if (!_itemIds.Contains(id))
				{
					_itemIds.add(id);
				}
			}
		}
	}
	
	public override void init()
	{
		GlobalEvents.Global.Subscribe<OnItemUse>(this, onItemUse);
	}
	
	public override bool isAvailable(Player player)
	{
		return base.isAvailable(player);
	}
	
	private void onItemUse(OnItemUse @event)
	{
		Player player = @event.getPlayer();
		if (player.getLevel() < _minLevel || player.getLevel() > _maxLevel || _itemIds.isEmpty())
		{
			return;
		}
		if (_itemIds.Contains(@event.getItem().getId()))
		{
			processPlayerProgress(player);
		}
	}
	
	private void processPlayerProgress(Player player)
	{
		player.getDailyMissions().addProgress(getHolder());
	}
}
