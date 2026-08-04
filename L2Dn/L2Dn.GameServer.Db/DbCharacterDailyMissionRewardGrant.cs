using Microsoft.EntityFrameworkCore;

namespace L2Dn.GameServer.Db;

[PrimaryKey(nameof(CharacterId), nameof(RewardId), nameof(CycleStart))]
public class DbCharacterDailyMissionRewardGrant
{
    public int CharacterId { get; set; }
    public int RewardId { get; set; }
    public DateTime CycleStart { get; set; }
    public byte DeliveryKind { get; set; }
    public int? MailMessageId { get; set; }
    public string RewardSnapshot { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime DeliveredAt { get; set; }
}
