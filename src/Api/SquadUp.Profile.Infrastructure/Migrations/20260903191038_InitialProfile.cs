using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Migration arguments are generated once per migration execution

namespace SquadUp.Profile.Infrastructure.Migrations;

/// <inheritdoc />
public partial class InitialProfile : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "profile");

        migrationBuilder.CreateTable(
            name: "games",
            schema: "profile",
            columns: table => new
            {
                id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                name = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_games", x => x.id);
            });

        migrationBuilder.CreateTable(
            name: "player_profiles",
            schema: "profile",
            columns: table => new
            {
                player_id = table.Column<Guid>(type: "uuid", nullable: false),
                nickname = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_player_profiles", x => x.player_id);
            });

        migrationBuilder.CreateTable(
            name: "rank_tiers",
            schema: "profile",
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
                table.ForeignKey(
                    name: "fk_rank_tiers_games_game_id",
                    column: x => x.game_id,
                    principalSchema: "profile",
                    principalTable: "games",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "player_games",
            schema: "profile",
            columns: table => new
            {
                player_id = table.Column<Guid>(type: "uuid", nullable: false),
                game_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                rank_tier_id = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                region = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                verified_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_player_games", x => new { x.player_id, x.game_id });
                table.ForeignKey(
                    name: "fk_player_games_games_game_id",
                    column: x => x.game_id,
                    principalSchema: "profile",
                    principalTable: "games",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_player_games_player_profiles_player_id",
                    column: x => x.player_id,
                    principalSchema: "profile",
                    principalTable: "player_profiles",
                    principalColumn: "player_id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "fk_player_games_rank_tiers_game_id_rank_tier_id",
                    columns: x => new { x.game_id, x.rank_tier_id },
                    principalSchema: "profile",
                    principalTable: "rank_tiers",
                    principalColumns: new[] { "game_id", "tier_id" },
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_player_games_game_id",
            schema: "profile",
            table: "player_games",
            column: "game_id");

        migrationBuilder.CreateIndex(
            name: "ix_player_games_game_id_rank_tier_id",
            schema: "profile",
            table: "player_games",
            columns: new[] { "game_id", "rank_tier_id" });

        migrationBuilder.CreateIndex(
            name: "ux_rank_tiers_game_id_ordinal",
            schema: "profile",
            table: "rank_tiers",
            columns: new[] { "game_id", "ordinal" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "player_games",
            schema: "profile");

        migrationBuilder.DropTable(
            name: "player_profiles",
            schema: "profile");

        migrationBuilder.DropTable(
            name: "rank_tiers",
            schema: "profile");

        migrationBuilder.DropTable(
            name: "games",
            schema: "profile");
    }
}
