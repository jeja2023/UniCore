using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations;

/// <summary>
/// 启用 pg_trgm 并为审计子串筛选列建立 GIN（gin_trgm_ops）索引，配合 ILIKE '%…%' 走索引友好路径。
/// </summary>
[Migration("20260418120000_EnablePgTrgmAuditEventSubstringIndexes")]
public partial class EnablePgTrgmAuditEventSubstringIndexes : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_AuditEvents_Actor_trgm"
            ON "AuditEvents" USING gin ("Actor" gin_trgm_ops);
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_AuditEvents_EventCode_trgm"
            ON "AuditEvents" USING gin ("EventCode" gin_trgm_ops);
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_AuditEvents_RequestPath_trgm"
            ON "AuditEvents" USING gin ("RequestPath" gin_trgm_ops)
            WHERE "RequestPath" IS NOT NULL;
            """);

        migrationBuilder.Sql(
            """
            CREATE INDEX IF NOT EXISTS "IX_AuditEvents_TraceId_trgm"
            ON "AuditEvents" USING gin ("TraceId" gin_trgm_ops)
            WHERE "TraceId" IS NOT NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AuditEvents_TraceId_trgm";""");
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AuditEvents_RequestPath_trgm";""");
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AuditEvents_EventCode_trgm";""");
        migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_AuditEvents_Actor_trgm";""");
    }
}
