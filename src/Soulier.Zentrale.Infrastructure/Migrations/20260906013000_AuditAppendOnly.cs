using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Soulier.Zentrale.Infrastructure.Migrations;

[DbContext(typeof(SoulierDbContext))]
[Migration("20260906013000_AuditAppendOnly")]
public sealed class AuditAppendOnly : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE OR REPLACE FUNCTION audit.prevent_event_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'audit.event is append-only' USING ERRCODE = '55000';
            END;
            $$;
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER audit_event_no_update_delete
            BEFORE UPDATE OR DELETE ON audit.event
            FOR EACH ROW
            EXECUTE FUNCTION audit.prevent_event_mutation();
            """);

        migrationBuilder.Sql("""
            CREATE TRIGGER audit_event_no_truncate
            BEFORE TRUNCATE ON audit.event
            FOR EACH STATEMENT
            EXECUTE FUNCTION audit.prevent_event_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_event_no_truncate ON audit.event;");
        migrationBuilder.Sql("DROP TRIGGER IF EXISTS audit_event_no_update_delete ON audit.event;");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS audit.prevent_event_mutation();");
    }
}
