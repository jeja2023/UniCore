using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Module.Abstractions.Contracts;

namespace Modules.SampleModule;

public sealed class SampleBusinessModule : IBusinessModule
{
    public ModuleMetadata Metadata => new("sample", "示例模块", "0.1.0");

    public IReadOnlyCollection<PermissionDeclaration> GetPermissions() =>
    [
        new("sample.read", "读取示例模块数据", "admin"),
        new("sample.update", "修改示例模块数据", "admin")
    ];

    public IReadOnlyCollection<MenuDeclaration> GetMenus() =>
    [
        new("menu.sample", "示例模块", "/sample", "sample.read")
    ];

    public IReadOnlyCollection<AuditDeclaration> GetAuditDeclarations() =>
    [
        new("sample.query", "查询示例数据", "Info")
    ];

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/modules/sample/ping", () => Results.Ok(new { ok = true, module = "sample" }));
    }
}
