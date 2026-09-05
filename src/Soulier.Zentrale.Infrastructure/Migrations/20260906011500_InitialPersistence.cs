using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulier.Zentrale.Infrastructure.Migrations;

[DbContext(typeof(SoulierDbContext))]
[Migration("20260906011500_InitialPersistence")]
public sealed class InitialPersistence : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "audit");
        migrationBuilder.EnsureSchema(name: "clients");
        migrationBuilder.EnsureSchema(name: "knowledge");

        migrationBuilder.CreateTable(
            name: "client",
            schema: "clients",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Environment = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_client", x => x.Id);
                table.CheckConstraint("CK_client_status", "\"Status\" IN (0, 1, 2, 3)");
                table.CheckConstraint("CK_client_name_nonempty", "length(btrim(\"Name\")) > 0");
                table.CheckConstraint("CK_client_environment_nonempty", "length(btrim(\"Environment\")) > 0");
            });

        migrationBuilder.CreateTable(
            name: "source",
            schema: "knowledge",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_source", x => x.Id);
                table.CheckConstraint("CK_source_name_nonempty", "length(btrim(\"Name\")) > 0");
                table.CheckConstraint("CK_source_type_nonempty", "length(btrim(\"SourceType\")) > 0");
            });

        migrationBuilder.CreateTable(
            name: "event",
            schema: "audit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                HumanPrincipalId = table.Column<Guid>(type: "uuid", nullable: true),
                ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                ServiceIdentityId = table.Column<Guid>(type: "uuid", nullable: true),
                CapabilityKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                ResourceType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ResourceId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: true),
                ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                PolicyVersion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                ApprovalId = table.Column<Guid>(type: "uuid", nullable: true),
                Result = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ReasonCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                SourceAdapter = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                DurationMs = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_event", x => x.Id);
                table.CheckConstraint("CK_event_duration_nonnegative", "\"DurationMs\" IS NULL OR \"DurationMs\" >= 0");
                table.CheckConstraint("CK_event_correlation_nonempty", "length(btrim(\"CorrelationId\")) > 0");
                table.CheckConstraint("CK_event_result_nonempty", "length(btrim(\"Result\")) > 0");
                table.CheckConstraint("CK_event_reason_nonempty", "length(btrim(\"ReasonCode\")) > 0");
            });

        migrationBuilder.CreateTable(
            name: "document",
            schema: "knowledge",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                KnowledgeSourceId = table.Column<Guid>(type: "uuid", nullable: false),
                LogicalName = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_document", x => x.Id);
                table.CheckConstraint("CK_document_logical_name_nonempty", "length(btrim(\"LogicalName\")) > 0");
                table.ForeignKey(
                    name: "FK_document_source_KnowledgeSourceId",
                    column: x => x.KnowledgeSourceId,
                    principalSchema: "knowledge",
                    principalTable: "source",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "document_version",
            schema: "knowledge",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                VersionNumber = table.Column<int>(type: "integer", nullable: false),
                ContentHash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                StorageProvider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                StorageKey = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                MimeType = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                TechnicalReviewStatus = table.Column<int>(type: "integer", nullable: false),
                SubjectReviewStatus = table.Column<int>(type: "integer", nullable: false),
                DataClassification = table.Column<int>(type: "integer", nullable: false),
                AiPolicy = table.Column<int>(type: "integer", nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedByHumanPrincipalId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_document_version", x => x.Id);
                table.CheckConstraint("CK_document_version_number_positive", "\"VersionNumber\" >= 1");
                table.CheckConstraint("CK_document_version_size_nonnegative", "\"SizeBytes\" >= 0");
                table.CheckConstraint("CK_document_version_technical_review", "\"TechnicalReviewStatus\" IN (0, 1, 2)");
                table.CheckConstraint("CK_document_version_subject_review", "\"SubjectReviewStatus\" IN (0, 1, 2)");
                table.CheckConstraint("CK_document_version_classification", "\"DataClassification\" IN (0, 1, 2, 3)");
                table.CheckConstraint("CK_document_version_ai_policy", "\"AiPolicy\" IN (0, 1, 2)");
                table.CheckConstraint("CK_document_version_hash_nonempty", "length(btrim(\"ContentHash\")) > 0");
                table.CheckConstraint("CK_document_version_storage_provider_nonempty", "length(btrim(\"StorageProvider\")) > 0");
                table.CheckConstraint("CK_document_version_storage_key_nonempty", "length(btrim(\"StorageKey\")) > 0");
                table.CheckConstraint("CK_document_version_mime_nonempty", "length(btrim(\"MimeType\")) > 0");
                table.ForeignKey(
                    name: "FK_document_version_document_DocumentId",
                    column: x => x.DocumentId,
                    principalSchema: "knowledge",
                    principalTable: "document",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "release",
            schema: "knowledge",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                DocumentVersionId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                ResourceScope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                UseCaseKey = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                ValidFromUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ValidUntilUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_release", x => x.Id);
                table.CheckConstraint("CK_release_status", "\"Status\" IN (0, 1, 2)");
                table.CheckConstraint("CK_release_window", "\"ValidUntilUtc\" IS NULL OR \"ValidUntilUtc\" > \"ValidFromUtc\"");
                table.CheckConstraint("CK_release_scope_nonempty", "length(btrim(\"ResourceScope\")) > 0");
                table.CheckConstraint("CK_release_use_case_nonempty", "length(btrim(\"UseCaseKey\")) > 0");
                table.ForeignKey(
                    name: "FK_release_client_ClientId",
                    column: x => x.ClientId,
                    principalSchema: "clients",
                    principalTable: "client",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_release_document_version_DocumentVersionId",
                    column: x => x.DocumentVersionId,
                    principalSchema: "knowledge",
                    principalTable: "document_version",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(
            name: "IX_client_Environment_Name",
            schema: "clients",
            table: "client",
            columns: new[] { "Environment", "Name" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_event_ClientId_OccurredAtUtc",
            schema: "audit",
            table: "event",
            columns: new[] { "ClientId", "OccurredAtUtc" });

        migrationBuilder.CreateIndex(
            name: "IX_event_CorrelationId",
            schema: "audit",
            table: "event",
            column: "CorrelationId");

        migrationBuilder.CreateIndex(
            name: "IX_event_OccurredAtUtc",
            schema: "audit",
            table: "event",
            column: "OccurredAtUtc");

        migrationBuilder.CreateIndex(
            name: "IX_document_KnowledgeSourceId_LogicalName",
            schema: "knowledge",
            table: "document",
            columns: new[] { "KnowledgeSourceId", "LogicalName" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_document_version_ContentHash",
            schema: "knowledge",
            table: "document_version",
            column: "ContentHash");

        migrationBuilder.CreateIndex(
            name: "IX_document_version_DocumentId_VersionNumber",
            schema: "knowledge",
            table: "document_version",
            columns: new[] { "DocumentId", "VersionNumber" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_release_ClientId_ResourceScope_UseCaseKey_Status",
            schema: "knowledge",
            table: "release",
            columns: new[] { "ClientId", "ResourceScope", "UseCaseKey", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_release_DocumentVersionId",
            schema: "knowledge",
            table: "release",
            column: "DocumentVersionId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "event", schema: "audit");
        migrationBuilder.DropTable(name: "release", schema: "knowledge");
        migrationBuilder.DropTable(name: "client", schema: "clients");
        migrationBuilder.DropTable(name: "document_version", schema: "knowledge");
        migrationBuilder.DropTable(name: "document", schema: "knowledge");
        migrationBuilder.DropTable(name: "source", schema: "knowledge");
    }
}
