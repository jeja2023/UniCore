using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Platform.Infrastructure.Persistence.Migrations
{
    public partial class AddMigrationsHistoryComments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                COMMENT ON TABLE "__EFMigrationsHistory" IS 'EF Core迁移历史表';
                COMMENT ON COLUMN "__EFMigrationsHistory"."MigrationId" IS '迁移标识';
                COMMENT ON COLUMN "__EFMigrationsHistory"."ProductVersion" IS 'EF Core版本';
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                COMMENT ON TABLE "__EFMigrationsHistory" IS NULL;
                COMMENT ON COLUMN "__EFMigrationsHistory"."MigrationId" IS NULL;
                COMMENT ON COLUMN "__EFMigrationsHistory"."ProductVersion" IS NULL;
                """);
        }
    }
}
