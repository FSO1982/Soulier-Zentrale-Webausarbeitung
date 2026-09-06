using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulier.Zentrale.Infrastructure.Migrations;

[DbContext(typeof(SoulierDbContext))]
[Migration("20260906040000_AddProviderAndApprovalState")]
public sealed class AddProviderAndApprovalState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "approval");

        migrationBuilder.CreateTable(
            name: "provider",
            schema: "ai",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Target = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                ApprovedByHumanPrincipalId = table.Column<Guid>(type: "uuid", nullable: true),
                ApprovedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_provider", x => x.Key);
                table.CheckConstraint("CK_provider_key_nonempty", "length(btrim(\"Key\")) > 0");
                table.CheckConstraint("CK_provider_target", "\"Target\" IN (0, 1)");
                table.CheckConstraint("CK_provider_status", "\"Status\" IN (0, 1, 2, 3)");
                table.CheckConstraint(
                    "CK_provider_external_approval_evidence",
                    "NOT (\"Target\" = 1 AND \"Status\" = 1) OR (\"ApprovedByHumanPrincipalId\" IS NOT NULL AND \"ApprovedAtUtc\" IS NOT NULL)");
                table.ForeignKey(
                    name: "FK_provider_human_principal_ApprovedByHumanPrincipalId",
                    column: x => x.ApprovedByHumanPrincipalId,
                    principalSchema: "identity",
                    principalTable: "human_principal",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "model_route",
            schema: "ai",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ProviderKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ModelAlias = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_model_route", x => x.Key);
                table.CheckConstraint("CK_model_route_key_nonempty", "length(btrim(\"Key\")) > 0");
                table.CheckConstraint("CK_model_route_provider_nonempty", "length(btrim(\"ProviderKey\")) > 0");
                table.CheckConstraint("CK_model_route_alias_nonempty", "length(btrim(\"ModelAlias\")) > 0");
                table.ForeignKey(
                    name: "FK_model_route_provider_ProviderKey",
                    column: x => x.ProviderKey,
                    principalSchema: "ai",
                    principalTable: "provider",
                    principalColumn: "Key",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "provider_use_case_grant",
            schema: "ai",
            columns: table => new
            {
                ProviderKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                UseCaseKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_provider_use_case_grant", x => new { x.ProviderKey, x.UseCaseKey });
                table.CheckConstraint("CK_provider_use_case_key_nonempty", "length(btrim(\"UseCaseKey\")) > 0");
                table.ForeignKey(
                    name: "FK_provider_use_case_grant_provider_ProviderKey",
                    column: x => x.ProviderKey,
                    principalSchema: "ai",
                    principalTable: "provider",
                    principalColumn: "Key",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "execution_approval",
            schema: "approval",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                HumanPrincipalId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                DecidedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ValidUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_execution_approval", x => x.Id);
                table.CheckConstraint("CK_execution_approval_action_nonempty", "length(btrim(\"ActionKey\")) > 0");
                table.CheckConstraint("CK_execution_approval_idempotency_nonempty", "length(btrim(\"IdempotencyKey\")) > 0");
                table.CheckConstraint("CK_execution_approval_status", "\"Status\" IN (0, 1, 2, 3)");
                table.CheckConstraint(
                    "CK_execution_approval_decision_evidence",
                    "\"Status\" <> 1 OR \"DecidedAtUtc\" IS NOT NULL");
                table.CheckConstraint(
                    "CK_execution_approval_validity",
                    "\"ValidUntilUtc\" IS NULL OR \"ValidUntilUtc\" > \"CreatedAtUtc\"");
                table.ForeignKey(
                    name: "FK_execution_approval_action_definition_ActionKey",
                    column: x => x.ActionKey,
                    principalSchema: "automation",
                    principalTable: "action_definition",
                    principalColumn: "Key",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_execution_approval_human_principal_HumanPrincipalId",
                    column: x => x.HumanPrincipalId,
                    principalSchema: "identity",
                    principalTable: "human_principal",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_provider_ApprovedByHumanPrincipalId",
            schema: "ai",
            table: "provider",
            column: "ApprovedByHumanPrincipalId");

        migrationBuilder.CreateIndex(
            name: "IX_provider_Status",
            schema: "ai",
            table: "provider",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_model_route_ProviderKey",
            schema: "ai",
            table: "model_route",
            column: "ProviderKey");

        migrationBuilder.CreateIndex(
            name: "IX_execution_approval_ActionKey_IdempotencyKey_approved",
            schema: "approval",
            table: "execution_approval",
            columns: new[] { "ActionKey", "IdempotencyKey" },
            unique: true,
            filter: "\"Status\" = 1");

        migrationBuilder.CreateIndex(
            name: "IX_execution_approval_HumanPrincipalId",
            schema: "approval",
            table: "execution_approval",
            column: "HumanPrincipalId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "execution_approval", schema: "approval");
        migrationBuilder.DropTable(name: "model_route", schema: "ai");
        migrationBuilder.DropTable(name: "provider_use_case_grant", schema: "ai");
        migrationBuilder.DropTable(name: "provider", schema: "ai");
    }
}
