using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Migration arguments are generated once per migration execution

namespace SquadUp.Profile.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SeedDota2Catalog : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "profile",
            table: "games",
            columns: new[] { "id", "is_active", "name" },
            values: new object[] { "dota2", true, "Dota 2" });

        migrationBuilder.InsertData(
            schema: "profile",
            table: "rank_tiers",
            columns: new[] { "game_id", "tier_id", "is_active", "name", "ordinal" },
            values: new object[,]
            {
                { "dota2", "ancient", true, "Ancient", 6 },
                { "dota2", "archon", true, "Archon", 4 },
                { "dota2", "crusader", true, "Crusader", 3 },
                { "dota2", "divine", true, "Divine", 7 },
                { "dota2", "guardian", true, "Guardian", 2 },
                { "dota2", "herald", true, "Herald", 1 },
                { "dota2", "immortal", true, "Immortal", 8 },
                { "dota2", "legend", true, "Legend", 5 }
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "profile",
            table: "rank_tiers",
            keyColumns: new[] { "game_id", "tier_id" },
            keyValues: new object[] { "dota2", "ancient" });

        migrationBuilder.DeleteData(
            schema: "profile",
            table: "rank_tiers",
            keyColumns: new[] { "game_id", "tier_id" },
            keyValues: new object[] { "dota2", "archon" });

        migrationBuilder.DeleteData(
            schema: "profile",
            table: "rank_tiers",
            keyColumns: new[] { "game_id", "tier_id" },
            keyValues: new object[] { "dota2", "crusader" });

        migrationBuilder.DeleteData(
            schema: "profile",
            table: "rank_tiers",
            keyColumns: new[] { "game_id", "tier_id" },
            keyValues: new object[] { "dota2", "divine" });

        migrationBuilder.DeleteData(
            schema: "profile",
            table: "rank_tiers",
            keyColumns: new[] { "game_id", "tier_id" },
            keyValues: new object[] { "dota2", "guardian" });

        migrationBuilder.DeleteData(
            schema: "profile",
            table: "rank_tiers",
            keyColumns: new[] { "game_id", "tier_id" },
            keyValues: new object[] { "dota2", "herald" });

        migrationBuilder.DeleteData(
            schema: "profile",
            table: "rank_tiers",
            keyColumns: new[] { "game_id", "tier_id" },
            keyValues: new object[] { "dota2", "immortal" });

        migrationBuilder.DeleteData(
            schema: "profile",
            table: "rank_tiers",
            keyColumns: new[] { "game_id", "tier_id" },
            keyValues: new object[] { "dota2", "legend" });

        migrationBuilder.DeleteData(
            schema: "profile",
            table: "games",
            keyColumn: "id",
            keyValue: "dota2");
    }
}
