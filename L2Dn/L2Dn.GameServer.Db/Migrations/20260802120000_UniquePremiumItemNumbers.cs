using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace L2Dn.GameServer.Db.Migrations;

[DbContext(typeof(GameServerDbContext))]
[Migration("20260802120000_UniquePremiumItemNumbers")]
public partial class UniquePremiumItemNumbers: Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            CREATE TEMP TABLE "__PremiumItemsResequenced" ON COMMIT DROP AS
            SELECT "CharacterId",
                   ROW_NUMBER() OVER (PARTITION BY "CharacterId" ORDER BY "ItemNumber", "ItemId")::integer AS "ItemNumber",
                   "ItemId", "ItemCount", "ItemSender"
            FROM "CharacterPremiumItems";

            DELETE FROM "CharacterPremiumItems";

            INSERT INTO "CharacterPremiumItems" ("CharacterId", "ItemNumber", "ItemId", "ItemCount", "ItemSender")
            SELECT "CharacterId", "ItemNumber", "ItemId", "ItemCount", "ItemSender"
            FROM "__PremiumItemsResequenced";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_CharacterPremiumItems_CharacterId_ItemNumber",
            table: "CharacterPremiumItems",
            columns: new[] { "CharacterId", "ItemNumber" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CharacterPremiumItems_CharacterId_ItemNumber",
            table: "CharacterPremiumItems");
    }
}
