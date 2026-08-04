using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L2Dn.GameServer.Db.Migrations;

[DbContext(typeof(GameServerDbContext))]
[Migration("20260804120000_DailyMissionRewardGrants")]
public partial class DailyMissionRewardGrants: Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CharacterDailyMissionRewardGrants",
            columns: table => new
            {
                CharacterId = table.Column<int>(type: "integer", nullable: false),
                RewardId = table.Column<int>(type: "integer", nullable: false),
                CycleStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeliveryKind = table.Column<byte>(type: "smallint", nullable: false),
                MailMessageId = table.Column<int>(type: "integer", nullable: true),
                RewardSnapshot = table.Column<string>(type: "text", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeliveredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CharacterDailyMissionRewardGrants",
                    x => new { x.CharacterId, x.RewardId, x.CycleStart });
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "CharacterDailyMissionRewardGrants");
    }
}
