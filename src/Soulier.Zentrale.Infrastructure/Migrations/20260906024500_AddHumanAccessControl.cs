using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulier.Zentrale.Infrastructure.Migrations;

[DbContext(typeof(SoulierDbContext))]
[Migration("20260906024500_AddHumanAccessControl")]
public sealed class AddHumanAccessControl : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "identity");

        migrationBuilder.CreateTable(
            name: "human_principal",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OidcSubject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                DisplayName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_human_principal", x => x.Id);
                table.CheckConstraint("CK_human_principal_status", "\"Status\" IN (0, 1)");
                table.CheckConstraint("CK_human_principal_subject_nonempty", "length(btrim(\"OidcSubject\")) > 0");
                table.CheckConstraint("CK_human_principal_display_name_nonempty", "length(btrim(\"DisplayName\")) > 0");
            });

        migrationBuilder.CreateTable(
            name: "role",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_role", x => x.Id);
                table.CheckConstraint("CK_role_key_nonempty", "length(btrim(\"Key\")) > 0");
                table.CheckConstraint("CK_role_name_nonempty", "length(btrim(\"Name\")) > 0");
            });

        migrationBuilder.CreateTable(
            name: "role_capability",
            schema: "identity",
            columns: table => new
            {
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                CapabilityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_role_capability", x => new { x.RoleId, x.CapabilityKey });
                table.CheckConstraint("CK_role_capability_key_nonempty", "length(btrim(\"CapabilityKey\")) > 0");
                table.ForeignKey(
                    name: "FK_role_capability_role_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "role",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "human_role_assignment",
            schema: "identity",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                HumanPrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                ResourceScope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Environment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                ValidFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ValidUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_human_role_assignment", x => x.Id);
                table.CheckConstraint("CK_human_role_assignment_status", "\"Status\" IN (0, 1)");
                table.CheckConstraint("CK_human_role_assignment_scope_nonempty", "length(btrim(\"ResourceScope\")) > 0");
                table.CheckConstraint("CK_human_role_assignment_environment_nonempty", "length(btrim(\"Environment\")) > 0");
                table.CheckConstraint("CK_human_role_assignment_window", "\"ValidUntilUtc\" IS NULL OR \"ValidUntilUtc\" > \"ValidFromUtc\"");
                table.ForeignKey(
                    name: "FK_human_role_assignment_human_principal_HumanPrincipalId",
                    column: x => x.HumanPrincipalId,
                    principalSchema: "identity",
                    principalTable: "human_principal",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_human_role_assignment_role_RoleId",
                    column: x => x.RoleId,
                    principalSchema: "identity",
                    principalTable: "role",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_human_principal_OidcSubject",
            schema: "identity",
            table: "human_principal",
            column: "OidcSubject",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_human_principal_Status",
            schema: "identity",
            table: "human_principal",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_role_Key",
            schema: "identity",
            table: "role",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_role_capability_CapabilityKey",
            schema: "identity",
            table: "role_capability",
            column: "CapabilityKey");

        migrationBuilder.CreateIndex(
            name: "IX_human_role_assignment_HumanPrincipalId_Status",
            schema: "identity",
            table: "human_role_assignment",
            columns: new[] { "HumanPrincipalId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_human_role_assignment_RoleId",
            schema: "identity",
            table: "human_role_assignment",
            column: "RoleId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "human_role_assignment", schema: "identity");
        migrationBuilder.DropTable(name: "role_capability", schema: "identity");
        migrationBuilder.DropTable(name: "human_principal", schema: "identity");
        migrationBuilder.DropTable(name: "role", schema: "identity");
    }
}
