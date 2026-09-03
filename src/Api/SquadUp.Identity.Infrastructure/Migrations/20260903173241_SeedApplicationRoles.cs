using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional
#pragma warning disable CA1861 // Migration arguments are generated once per migration execution

namespace SquadUp.Identity.Infrastructure.Migrations;

/// <inheritdoc />
public partial class SeedApplicationRoles : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.InsertData(
            schema: "identity",
            table: "roles",
            columns: new[] { "id", "concurrency_stamp", "name", "normalized_name" },
            values: new object[,]
            {
                { new Guid("01990a98-6380-7000-8000-000000000001"), null, "Player", "PLAYER" },
                { new Guid("01990a98-6380-7000-8000-000000000002"), null, "Moderator", "MODERATOR" },
                { new Guid("01990a98-6380-7000-8000-000000000003"), null, "Admin", "ADMIN" }
            });

        migrationBuilder.Sql(
            """
            INSERT INTO identity.user_roles (user_id, role_id)
            SELECT id, '01990a98-6380-7000-8000-000000000001'::uuid
            FROM identity.users
            ON CONFLICT (user_id, role_id) DO NOTHING;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "id",
            keyValue: new Guid("01990a98-6380-7000-8000-000000000001"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "id",
            keyValue: new Guid("01990a98-6380-7000-8000-000000000002"));

        migrationBuilder.DeleteData(
            schema: "identity",
            table: "roles",
            keyColumn: "id",
            keyValue: new Guid("01990a98-6380-7000-8000-000000000003"));
    }
}
