using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Platform.Infrastructure.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
        var webApiDirectory = ResolveWebApiDirectory();
        var configuration = new ConfigurationBuilder()
            .SetBasePath(webApiDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default 未配置，无法创建设计时 DbContext。");

        optionsBuilder.UseNpgsql(connectionString);
        optionsBuilder.ReplaceService<IHistoryRepository, CommentedNpgsqlHistoryRepository>();
        return new AppDbContext(optionsBuilder.Options);
    }

    private static string ResolveWebApiDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "src", "Platform.WebApi"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "Platform.WebApi"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Platform.WebApi")
        };

        foreach (var candidate in candidates)
        {
            var fullPath = Path.GetFullPath(candidate);
            if (File.Exists(Path.Combine(fullPath, "appsettings.json")))
            {
                return fullPath;
            }
        }

        throw new DirectoryNotFoundException("无法定位 Platform.WebApi 目录，请在仓库内执行 dotnet ef。");
    }
}
