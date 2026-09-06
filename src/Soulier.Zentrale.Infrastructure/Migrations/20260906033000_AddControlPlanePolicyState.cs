using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulier.Zentrale.Infrastructure.Migrations;

[DbContext(typeof(SoulierDbContext))]
[Migration("20260906033000_AddControlPlanePolicyState")]
public sealed class AddControlPlanePolicyState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "ai");
        migrationBuilder.EnsureSchema(name: "automation");
        migrationBuilder.EnsureSchema(name: "policy");

        migrationBuilder.CreateTable(
            name: "use_case",
            schema: "ai",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_use_case", x => x.Id);
                table.CheckConstraint("CK_use_case_key_nonempty", "length(btrim(\"Key\")) > 0");
                table.CheckConstraint("CK_use_case_name_nonempty", "length(btrim(\"Name\")) > 0");
            });

        migrationBuilder.CreateTable(
            name: "action_definition",
            schema: "automation",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Mode = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                ParameterPolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_action_definition", x => x.Key);
                table.CheckConstraint("CK_action_definition_key_nonempty", "length(btrim(\"Key\")) > 0");
                table.CheckConstraint("CK_action_definition_mode", "\"Mode\" IN (0, 1, 2, 3)");
            });

        migrationBuilder.CreateTable(
            name: "retention_rule",
            schema: "policy",
            columns: table => new
            {
                Category = table.Column<int>(type: "integer", nullable: false),
                RetainFor = table.Column<TimeSpan>(type: "interval", nullable: true),
                DeletionEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LegalHoldSupported = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_retention_rule", x => x.Category);
                table.CheckConstraint("CK_retention_rule_category", "\"Category\" IN (0, 1, 2, 3, 4, 5)");
                table.CheckConstraint("CK_retention_rule_period", "\"RetainFor\" IS NULL OR \"RetainFor\" > interval '0 seconds'");
                table.CheckConstraint("CK_retention_rule_delete_requires_period", "NOT \"DeletionEnabled\" OR \"RetainFor\" IS NOT NULL");
            });

        migrationBuilder.CreateTable(
            name: "use_case_version",
            schema: "ai",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AiUseCaseId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionNumber = table.Column<int>(type: "integer", nullable: false),
                PromptTemplateHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                ModelRouteKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                TechnicalReviewStatus = table.Column<int>(type: "integer", nullable: false),
                SubjectReviewStatus = table.Column<int>(type: "integer", nullable: false),
                ContentLoggingMode = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ActivatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_use_case_version", x => x.Id);
                table.CheckConstraint("CK_use_case_version_number", "\"VersionNumber\" > 0");
                table.CheckConstraint("CK_use_case_version_prompt_hash_nonempty", "length(btrim(\"PromptTemplateHash\")) > 0");
                table.CheckConstraint("CK_use_case_version_model_route_nonempty", "length(btrim(\"ModelRouteKey\")) > 0");
                table.CheckConstraint("CK_use_case_version_status", "\"Status\" IN (0, 1, 2)");
                table.CheckConstraint("CK_use_case_version_technical_review", "\"TechnicalReviewStatus\" IN (0, 1, 2)");
                table.CheckConstraint("CK_use_case_version_subject_review", "\"SubjectReviewStatus\" IN (0, 1, 2)");
                table.CheckConstraint("CK_use_case_version_logging", "\"ContentLoggingMode\" IN (0, 1)");
                table.ForeignKey(
                    name: "FK_use_case_version_use_case_AiUseCaseId",
                    column: x => x.AiUseCaseId,
                    principalSchema: "ai",
                    principalTable: "use_case",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "action_execution",
            schema: "automation",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ActionKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                IdempotencyKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                ResourceScope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                State = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CompletedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ResultReference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_action_execution", x => x.Id);
                table.CheckConstraint("CK_action_execution_action_key_nonempty", "length(btrim(\"ActionKey\")) > 0");
                table.CheckConstraint("CK_action_execution_idempotency_nonempty", "length(btrim(\"IdempotencyKey\")) > 0");
                table.CheckConstraint("CK_action_execution_scope_nonempty", "length(btrim(\"ResourceScope\")) > 0");
                table.CheckConstraint("CK_action_execution_correlation_nonempty", "length(btrim(\"CorrelationId\")) > 0");
                table.CheckConstraint("CK_action_execution_state", "\"State\" IN (1, 2, 3)");
                table.CheckConstraint("CK_action_execution_completion", "\"CompletedAtUtc\" IS NULL OR \"CompletedAtUtc\" >= \"CreatedAtUtc\"");
                table.ForeignKey(
                    name: "FK_action_execution_action_definition_ActionKey",
                    column: x => x.ActionKey,
                    principalSchema: "automation",
                    principalTable: "action_definition",
                    principalColumn: "Key",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_use_case_Key",
            schema: "ai",
            table: "use_case",
            column: "Key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_use_case_version_AiUseCaseId_VersionNumber",
            schema: "ai",
            table: "use_case_version",
            columns: new[] { "AiUseCaseId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_use_case_version_AiUseCaseId_active",
            schema: "ai",
            table: "use_case_version",
            column: "AiUseCaseId",
            unique: true,
            filter: "\"Status\" = 1");

        migrationBuilder.CreateIndex(
            name: "IX_action_definition_Mode",
            schema: "automation",
            table: "action_definition",
            column: "Mode");

        migrationBuilder.CreateIndex(
            name: "IX_action_execution_ActionKey_IdempotencyKey",
            schema: "automation",
            table: "action_execution",
            columns: new[] { "ActionKey", "IdempotencyKey" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_action_execution_CorrelationId",
            schema: "automation",
            table: "action_execution",
            column: "CorrelationId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "use_case_version", schema: "ai");
        migrationBuilder.DropTable(name: "action_execution", schema: "automation");
        migrationBuilder.DropTable(name: "retention_rule", schema: "policy");
        migrationBuilder.DropTable(name: "use_case", schema: "ai");
        migrationBuilder.DropTable(name: "action_definition", schema: "automation");
    }
}
