var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { ok = true, service = "UniCorePlatformStarter" }));

app.Run();
