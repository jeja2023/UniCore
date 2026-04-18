using System.Reflection;
using System.Runtime.Loader;
using Platform.Module.Abstractions.Contracts;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var moduleContracts = DiscoverModuleContracts();

app.MapGet("/api/health", () => Results.Ok(new { ok = true, service = "UniCorePlatformStarter" }));
app.MapGet("/swagger/v1/swagger.json", () => Results.Ok(new { openapi = "3.0.1", info = new { title = "UniCore Platform", version = "v1" } }));

app.MapPost("/api/auth/login", () =>
{
    return Results.Ok(new
    {
        data = new
        {
            accessToken = "bootstrap-e2e-token"
        }
    });
});

app.MapGet("/api/modules/contracts", (HttpContext httpContext) =>
{
    if (!HasBearerToken(httpContext))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new { data = moduleContracts });
});

app.MapGet("/api/modules/contracts/report", (HttpContext httpContext) =>
{
    if (!HasBearerToken(httpContext))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(new { data = new { isValid = true } });
});

app.Run();

static bool HasBearerToken(HttpContext httpContext)
{
    var authHeader = httpContext.Request.Headers.Authorization.ToString();
    return authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);
}

static IReadOnlyList<object> DiscoverModuleContracts()
{
    var moduleType = typeof(IBusinessModule);
    var contracts = new List<object>();

    var candidateAssemblies = new List<Assembly>();
    candidateAssemblies.AddRange(
        AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a => !a.IsDynamic));

    foreach (var reference in Assembly.GetExecutingAssembly().GetReferencedAssemblies())
    {
        try
        {
            var loaded = Assembly.Load(reference);
            candidateAssemblies.Add(loaded);
        }
        catch
        {
            // Ignore optional assemblies that cannot be loaded.
        }
    }

    foreach (var dllPath in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
    {
        try
        {
            var loaded = AssemblyLoadContext.Default.LoadFromAssemblyPath(dllPath);
            candidateAssemblies.Add(loaded);
        }
        catch
        {
            // Ignore non-.NET assemblies or locked files.
        }
    }

    foreach (var assembly in candidateAssemblies.DistinctBy(a => a.FullName))
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Cast<Type>().ToArray();
        }

        foreach (var type in types)
        {
            if (type.IsAbstract || type.IsInterface || !moduleType.IsAssignableFrom(type))
            {
                continue;
            }

            if (Activator.CreateInstance(type) is not IBusinessModule module)
            {
                continue;
            }

            contracts.Add(new
            {
                moduleCode = module.Metadata.ModuleCode,
                permissions = module.GetPermissions().Select(p => new { permissionCode = p.PermissionCode }).ToArray(),
                menus = module.GetMenus().Select(m => new
                {
                    menuCode = m.MenuCode,
                    routePath = m.RoutePath,
                    permissionCode = m.PermissionCode
                }).ToArray()
            });
        }
    }

    return contracts;
}
