using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814, CA1861, IDE0161 // Generated migration code.

namespace SquadUp.LobbyService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLobby : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "lobby");

            migrationBuilder.CreateTable(
                name: "game_catalog",
                schema: "lobby",
                columns: table => new
                {
                    id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_catalog", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lobbies",
                schema: "lobby",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    game_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    minimum_ordinal = table.Column<int>(type: "integer", nullable: false),
                    maximum_ordinal = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    members_count = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lobbies", x => x.id);
                    table.CheckConstraint("ck_lobbies_capacity_range", "capacity >= 2 AND capacity <= 100");
                    table.CheckConstraint("ck_lobbies_member_count_range", "members_count >= 0 AND members_count <= capacity");
                    table.CheckConstraint("ck_lobbies_rank_range", "minimum_ordinal > 0 AND (maximum_ordinal IS NULL OR maximum_ordinal >= minimum_ordinal)");
                    table.CheckConstraint("ck_lobbies_status", "status IN ('Recruiting', 'Full', 'Provisioning', 'Ready', 'Cancelled', 'Completed', 'Expired')");
                });

            migrationBuilder.CreateTable(
                name: "rank_tiers",
                schema: "lobby",
                columns: table => new
                {
                    game_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    tier_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rank_tiers", x => new { x.game_id, x.tier_id });
                    table.CheckConstraint("ck_rank_tiers_ordinal_positive", "ordinal > 0");
                    table.ForeignKey(
                        name: "fk_rank_tiers_game_catalog_game_id",
                        column: x => x.game_id,
                        principalSchema: "lobby",
                        principalTable: "game_catalog",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lobby_members",
                schema: "lobby",
                columns: table => new
                {
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lobby_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discord_user_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    display_name = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rank_game_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rank_ordinal = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lobby_members", x => new { x.lobby_id, x.player_id });
                    table.CheckConstraint("ck_lobby_members_discord_user_id", "char_length(discord_user_id) BETWEEN 1 AND 32");
                    table.CheckConstraint("ck_lobby_members_display_name", "char_length(display_name) BETWEEN 1 AND 32");
                    table.CheckConstraint("ck_lobby_members_rank_ordinal_positive", "rank_ordinal > 0");
                    table.ForeignKey(
                        name: "FK_lobby_members_lobbies_lobby_id",
                        column: x => x.lobby_id,
                        principalSchema: "lobby",
                        principalTable: "lobbies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "lobby",
                table: "game_catalog",
                columns: new[] { "id", "is_active", "name" },
                values: new object[] { "dota2", true, "Dota 2" });

            migrationBuilder.InsertData(
                schema: "lobby",
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

            migrationBuilder.CreateIndex(
                name: "ux_rank_tiers_game_id_ordinal",
                schema: "lobby",
                table: "rank_tiers",
                columns: new[] { "game_id", "ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lobby_members",
                schema: "lobby");

            migrationBuilder.DropTable(
                name: "rank_tiers",
                schema: "lobby");

            migrationBuilder.DropTable(
                name: "lobbies",
                schema: "lobby");

            migrationBuilder.DropTable(
                name: "game_catalog",
                schema: "lobby");
        }
    }
}
