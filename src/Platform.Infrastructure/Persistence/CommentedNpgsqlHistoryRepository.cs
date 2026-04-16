using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Migrations.Internal;

namespace Platform.Infrastructure.Persistence;

#pragma warning disable EF1001
public sealed class CommentedNpgsqlHistoryRepository(HistoryRepositoryDependencies dependencies)
    : NpgsqlHistoryRepository(dependencies)
{
    protected override void ConfigureTable(EntityTypeBuilder<HistoryRow> history)
    {
        base.ConfigureTable(history);
        history.ToTable(t => t.HasComment("EF Core迁移历史表"));
        history.Property(x => x.MigrationId).HasComment("迁移标识");
        history.Property(x => x.ProductVersion).HasComment("EF Core版本");
    }
}
#pragma warning restore EF1001
