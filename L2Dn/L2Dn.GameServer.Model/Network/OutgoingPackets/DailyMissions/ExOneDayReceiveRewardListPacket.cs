using L2Dn.GameServer.Data.Xml;
using L2Dn.GameServer.Db;
using L2Dn.GameServer.Model;
using L2Dn.GameServer.Model.Actor;
using L2Dn.GameServer.Model.DailyMissions;
using L2Dn.Model.Enums;
using L2Dn.Packets;

namespace L2Dn.GameServer.Network.OutgoingPackets.DailyMissions;

public readonly struct ExOneDayReceiveRewardListPacket: IOutgoingPacket
{
    //private static final SchedulingPattern DAILY_REUSE_PATTERN = new SchedulingPattern("30 6 * * *");
    //private static final SchedulingPattern WEEKLY_REUSE_PATTERN = new SchedulingPattern("30 6 * * 1");
    //private static final SchedulingPattern MONTHLY_REUSE_PATTERN = new SchedulingPattern("30 6 1 * *");

    private readonly Player _player;
    private readonly ICollection<DailyMissionDataHolder> _rewards;
    private readonly int _dayRemainTimeInSeconds;
    private readonly int _weekRemainTimeInSeconds;
    private readonly int _monthRemainTimeInSeconds;

    public ExOneDayReceiveRewardListPacket(Player player, bool sendRewards)
    {
        _player = player;
        _rewards = sendRewards ? DailyMissionData.getInstance().getDailyMissionData(player) : new List<DailyMissionDataHolder>();

        DateTimeOffset now = DateTimeOffset.Now;
        _dayRemainTimeInSeconds = DailyMissionCycle.getRemainingSeconds(MissionResetType.DAY, now);
        _weekRemainTimeInSeconds = DailyMissionCycle.getRemainingSeconds(MissionResetType.WEEK, now);
        _monthRemainTimeInSeconds = DailyMissionCycle.getRemainingSeconds(MissionResetType.MONTH, now);
    }

    public void WriteContent(PacketBitWriter writer)
    {
        if (!DailyMissionData.getInstance().isAvailable())
        {
            return;
        }

        writer.WritePacketCode(OutgoingPacketCodes.EX_ONE_DAY_RECEIVE_REWARD_LIST);

        writer.WriteInt32(_dayRemainTimeInSeconds);
        writer.WriteInt32(_weekRemainTimeInSeconds);
        writer.WriteInt32(_monthRemainTimeInSeconds);
        writer.WriteByte(0x17);
        writer.WriteInt32((int)_player.getClassId());
        writer.WriteInt32((int)DateTime.Now.DayOfWeek); // Day of week
        writer.WriteInt32(_rewards.Count);

        foreach (DailyMissionDataHolder reward in _rewards)
        {
            writer.WriteInt16((short)reward.getId());

            DailyMissionStatus status = reward.getStatus(_player);
            writer.WriteByte((byte)status);
            writer.WriteByte(reward.getRequiredCompletions() > 1);
            writer.WriteInt32(reward.getParams().getInt("level", -1) == -1
                ? status == DailyMissionStatus.AVAILABLE ? 0 : reward.getProgress(_player)
                : _player.getLevel());

            writer.WriteInt32(reward.getRequiredCompletions());
        }
    }
}
