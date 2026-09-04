using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable IDE0161 // Generated migration code.

namespace SquadUp.LobbyService.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHttpIdempotencyLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "http_idempotency_keys",
                schema: "lobby",
                columns: table => new
                {
                    owner_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    request_hash = table.Column<byte[]>(type: "bytea", maxLength: 32, nullable: false),
                    expires_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    response_status_code = table.Column<int>(type: "integer", nullable: true),
                    response_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    response_detail = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    response_location = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    response_body = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_http_idempotency_keys", x => new { x.owner_player_id, x.key });
                    table.CheckConstraint("ck_http_idempotency_keys_hash_length", "octet_length(request_hash) = 32");
                    table.CheckConstraint("ck_http_idempotency_keys_key_length", "char_length(key) BETWEEN 1 AND 128");
                    table.CheckConstraint("ck_http_idempotency_keys_response_status", "response_status_code IS NULL OR response_status_code BETWEEN 100 AND 599");
                });

            migrationBuilder.CreateIndex(
                name: "ix_http_idempotency_keys_expires_at_utc",
                schema: "lobby",
                table: "http_idempotency_keys",
                column: "expires_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "http_idempotency_keys",
                schema: "lobby");
        }
    }
}
