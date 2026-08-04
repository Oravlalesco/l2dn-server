using L2Dn.GameServer.Db;
using L2Dn.GameServer.Handlers;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.Events;
using L2Dn.GameServer.Model.Events.Impl.Olympiads;
using L2Dn.GameServer.Model.Olympiads;

namespace L2Dn.GameServer.Scripts.Handlers.DailyMissionHandlers;

/**
 * @author UnAfraid
 */
public class OlympiadDailyMissionHandler: AbstractDailyMissionHandler
{
	private readonly int _amount;
	private readonly bool _winOnly;

	public OlympiadDailyMissionHandler(DailyMissionDataHolder holder): base(holder)
	{
		_amount = holder.getRequiredCompletions();
		_winOnly = holder.getParams().getBoolean("winOnly", false);
	}

	public override void init()
	{
		GlobalEvents.Global.Subscribe<OnOlympiadMatchResult>(this, onOlympiadMatchResult);
	}

	public override bool isAvailable(Player player)
	{
		return base.isAvailable(player);
	}

	private void onOlympiadMatchResult(OnOlympiadMatchResult @event)
    {
        Participant? winner = @event.getWinner();
		if (winner != null)
		{
			Player player = winner.getPlayer();
			player.getDailyMissions().addProgress(getHolder());
		}

		if (!_winOnly && @event.getLoser() != null)
		{
			Player player = @event.getLoser().getPlayer();
			player.getDailyMissions().addProgress(getHolder());
		}
	}
}
