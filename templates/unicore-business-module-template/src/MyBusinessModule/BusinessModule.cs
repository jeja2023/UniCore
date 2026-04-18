using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Platform.Module.Abstractions.Contracts;

namespace MyBusinessModule;

public sealed class BusinessModule : IBusinessModule
{
    public ModuleMetadata Metadata => new("ModuleCode", "My Business Module", "0.1.0");

    public void RegisterServices(IServiceCollection services)
    {
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/modules/ModuleCode/ping", () => Results.Ok(new { ok = true, module = "ModuleCode" }));
    }

    public IReadOnlyCollection<PermissionDeclaration> GetPermissions() =>
    [
        new("ModuleCode.read", "读取模块数据", "admin")
    ];

    public IReadOnlyCollection<MenuDeclaration> GetMenus() =>
    [
        new("menu.ModuleCode", "My Module", "/ModuleCode", "ModuleCode.read")
    ];

    public IReadOnlyCollection<AuditDeclaration> GetAuditDeclarations() =>
    [
        new("ModuleCode.query", "查询模块数据", "Info")
    ];

    public IReadOnlyCollection<MigrationDeclaration> GetMigrations() =>
    [
        new("20260101000000_ModuleCode_Initial", "模块初始化迁移")
    ];

    public IReadOnlyCollection<EventDeclaration> GetEvents() =>
    [
        new("ModuleCode.changed", "模块数据变更事件")
    ];
}
