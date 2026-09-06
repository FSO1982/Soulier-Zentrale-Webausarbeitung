using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulier.Zentrale.Infrastructure.Migrations;

[DbContext(typeof(SoulierDbContext))]
[Migration("20260906043000_AddPersistentCapabilities")]
public sealed class AddPersistentCapabilities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "capability",
            schema: "clients",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                MajorVersion = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_capability", x => new { x.Key, x.MajorVersion });
                table.CheckConstraint("CK_capability_key_nonempty", "length(btrim(\"Key\")) > 0");
                table.CheckConstraint("CK_capability_major_version", "\"MajorVersion\" > 0");
            });

        migrationBuilder.CreateTable(
            name: "grant",
            schema: "clients",
            columns: table => new
            {
                ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                CapabilityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ResourceScope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Environment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                ValidFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ValidUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CapabilityMajorVersion = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey(
                    "PK_grant",
                    x => new
                    {
                        x.ClientId,
                        x.CapabilityKey,
                        x.CapabilityMajorVersion,
                        x.ResourceScope,
                        x.Environment
                    });
                table.CheckConstraint("CK_grant_capability_key_nonempty", "length(btrim(\"CapabilityKey\")) > 0");
                table.CheckConstraint("CK_grant_major_version", "\"CapabilityMajorVersion\" > 0");
                table.CheckConstraint("CK_grant_scope_nonempty", "length(btrim(\"ResourceScope\")) > 0");
                table.CheckConstraint("CK_grant_environment_nonempty", "length(btrim(\"Environment\")) > 0");
                table.CheckConstraint("CK_grant_status", "\"Status\" IN (0, 1)");
                table.CheckConstraint("CK_grant_window", "\"ValidUntilUtc\" IS NULL OR \"ValidUntilUtc\" > \"ValidFromUtc\"");
                table.ForeignKey(
                    name: "FK_grant_client_ClientId",
                    column: x => x.ClientId,
                    principalSchema: "clients",
                    principalTable: "client",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_grant_capability_CapabilityKey_CapabilityMajorVersion",
                    columns: x => new { x.CapabilityKey, x.CapabilityMajorVersion },
                    principalSchema: "clients",
                    principalTable: "capability",
                    principalColumns: new[] { "Key", "MajorVersion" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_capability_IsActive",
            schema: "clients",
            table: "capability",
            column: "IsActive");

        migrationBuilder.CreateIndex(
            name: "IX_grant_CapabilityKey_CapabilityMajorVersion",
            schema: "clients",
            table: "grant",
            columns: new[] { "CapabilityKey", "CapabilityMajorVersion" });

        migrationBuilder.CreateIndex(
            name: "IX_grant_ClientId_Status",
            schema: "clients",
            table: "grant",
            columns: new[] { "ClientId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "grant", schema: "clients");
        migrationBuilder.DropTable(name: "capability", schema: "clients");
    }
}
