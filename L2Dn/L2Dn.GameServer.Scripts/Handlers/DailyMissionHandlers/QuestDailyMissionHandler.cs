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
public class QuestDailyMissionHandler: AbstractDailyMissionHandler
{
	private readonly int _amount;
	
	public QuestDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
	{
		_amount = holder.getRequiredCompletions();
	}
	
	public override void init()
	{
		GlobalEvents.Players.Subscribe<OnPlayerQuestComplete>(this, onQuestComplete);
	}
	
	public override bool isAvailable(Player player)
	{
		return base.isAvailable(player);
	}
	
	private void onQuestComplete(OnPlayerQuestComplete @event)
	{
		Player player = @event.getPlayer();
		if (@event.getQuestType() == QuestType.DAILY)
		{
			player.getDailyMissions().addProgress(getHolder());
		}
	}
}
