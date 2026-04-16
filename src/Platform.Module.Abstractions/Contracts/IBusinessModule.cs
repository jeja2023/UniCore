using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Platform.Module.Abstractions.Contracts;

public interface IBusinessModule
{
    ModuleMetadata Metadata { get; }

    void RegisterServices(IServiceCollection services);

    void MapEndpoints(IEndpointRouteBuilder endpoints);

    IReadOnlyCollection<PermissionDeclaration> GetPermissions();

    IReadOnlyCollection<MenuDeclaration> GetMenus();

    IReadOnlyCollection<AuditDeclaration> GetAuditDeclarations();

    IReadOnlyCollection<MigrationDeclaration> GetMigrations() => Array.Empty<MigrationDeclaration>();

    IReadOnlyCollection<EventDeclaration> GetEvents() => Array.Empty<EventDeclaration>();
}
