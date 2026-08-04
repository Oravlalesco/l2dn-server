using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L2Dn.GameServer.Db.Migrations;

[DbContext(typeof(GameServerDbContext))]
[Migration("20260803120000_DailyMissionCycles")]
public partial class DailyMissionCycles: Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "CycleStart",
            table: "CharacterDailyRewards",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: DateTime.UnixEpoch);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CycleStart",
            table: "CharacterDailyRewards");
    }
}
