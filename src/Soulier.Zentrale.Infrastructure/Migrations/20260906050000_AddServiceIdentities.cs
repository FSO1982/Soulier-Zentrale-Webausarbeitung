using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulier.Zentrale.Infrastructure.Migrations;

[DbContext(typeof(SoulierDbContext))]
[Migration("20260906050000_AddServiceIdentities")]
public sealed class AddServiceIdentities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "service_identity",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Environment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_service_identity", x => x.Id);
                table.CheckConstraint("CK_service_identity_name_nonempty", "length(btrim(\"Name\")) > 0");
                table.CheckConstraint("CK_service_identity_environment_nonempty", "length(btrim(\"Environment\")) > 0");
                table.CheckConstraint("CK_service_identity_status", "\"Status\" IN (0, 1, 2, 3)");
            });

        migrationBuilder.CreateTable(
            name: "service_grant",
            schema: "identity",
            columns: table => new
            {
                ServiceIdentityId = table.Column<Guid>(type: "uuid", nullable: false),
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
                    "PK_service_grant",
                    x => new
                    {
                        x.ServiceIdentityId,
                        x.CapabilityKey,
                        x.CapabilityMajorVersion,
                        x.ResourceScope,
                        x.Environment
                    });
                table.CheckConstraint("CK_service_grant_capability_nonempty", "length(btrim(\"CapabilityKey\")) > 0");
                table.CheckConstraint("CK_service_grant_major_version", "\"CapabilityMajorVersion\" > 0");
                table.CheckConstraint("CK_service_grant_scope_nonempty", "length(btrim(\"ResourceScope\")) > 0");
                table.CheckConstraint("CK_service_grant_environment_nonempty", "length(btrim(\"Environment\")) > 0");
                table.CheckConstraint("CK_service_grant_status", "\"Status\" IN (0, 1)");
                table.CheckConstraint("CK_service_grant_window", "\"ValidUntilUtc\" IS NULL OR \"ValidUntilUtc\" > \"ValidFromUtc\"");
                table.ForeignKey(
                    name: "FK_service_grant_service_identity_ServiceIdentityId",
                    column: x => x.ServiceIdentityId,
                    principalSchema: "identity",
                    principalTable: "service_identity",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_service_grant_capability_CapabilityKey_CapabilityMajorVersion",
                    columns: x => new { x.CapabilityKey, x.CapabilityMajorVersion },
                    principalSchema: "clients",
                    principalTable: "capability",
                    principalColumns: new[] { "Key", "MajorVersion" },
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_service_identity_Environment_Name",
            schema: "identity",
            table: "service_identity",
            columns: new[] { "Environment", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_service_identity_Status",
            schema: "identity",
            table: "service_identity",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_service_grant_CapabilityKey_CapabilityMajorVersion",
            schema: "identity",
            table: "service_grant",
            columns: new[] { "CapabilityKey", "CapabilityMajorVersion" });

        migrationBuilder.CreateIndex(
            name: "IX_service_grant_ServiceIdentityId_Status",
            schema: "identity",
            table: "service_grant",
            columns: new[] { "ServiceIdentityId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "service_grant", schema: "identity");
        migrationBuilder.DropTable(name: "service_identity", schema: "identity");
    }
}
