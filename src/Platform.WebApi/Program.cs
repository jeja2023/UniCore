using System.Reflection;
using Microsoft.Extensions.Options;
using Platform.Auth.Services;
using Platform.Infrastructure.Persistence;
using Platform.WebApi.Endpoints;
using Platform.WebApi.Middleware;
using Platform.WebApi.Metrics;
using Platform.WebApi.Startup;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPlatformWebApiServices(builder.Configuration);
var discoveredModules = builder.Services.AddDiscoveredBusinessModules(Assembly.GetExecutingAssembly());

var app = builder.Build();
var jwtOptions = app.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
var useInMemoryDatabase = builder.Configuration.GetValue<bool>("UseInMemoryDatabase");
var configuredAdminPassword =
    Environment.GetEnvironmentVariable("UNICORE_ADMIN_PASSWORD")
    ?? builder.Configuration["Seed:AdminPassword"];

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var recreateOnStartup = string.Equals(
        Environment.GetEnvironmentVariable("UNICORE_RECREATE_ON_STARTUP"),
        "true",
        StringComparison.OrdinalIgnoreCase);
    await DbSeeder.SeedAsync(
        dbContext,
        recreateOnStartup,
        configuredAdminPassword,
        allowDefaultAdminPassword: app.Environment.IsDevelopment() || useInMemoryDatabase);
}

var enableSwagger = app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("Swagger:EnableInNonDevelopment");
if (enableSwagger)
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseMiddleware<RequestMetricsMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<RequestAuditMiddleware>();

app.Use(async (context, next) =>
{
    context.TraceIdentifier = context.TraceIdentifier.Length == 0
        ? Guid.NewGuid().ToString("N")
        : context.TraceIdentifier;
    await next();
});

app.MapPlatformFoundationEndpoints();
app.MapSecurityEndpoints(jwtOptions);
app.MapAuditAndModuleEndpoints(discoveredModules);
app.MapExpansionGovernanceEndpoints();

app.MapGroup("/api/modules").WithTags("Modules");
foreach (var module in discoveredModules)
{
    module.MapEndpoints(app);
}

app.Run();

public partial class Program;
