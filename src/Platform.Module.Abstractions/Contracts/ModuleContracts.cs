namespace Platform.Module.Abstractions.Contracts;

public sealed record ModuleMetadata(string ModuleCode, string ModuleName, string ModuleVersion);

public sealed record PermissionDeclaration(string PermissionCode, string Description, string? DefaultRole = null);

public sealed record MenuDeclaration(string MenuCode, string Title, string RoutePath, string PermissionCode);

public sealed record AuditDeclaration(string EventCode, string Description, string Level);

public sealed record MigrationDeclaration(string MigrationId, string Description);

public sealed record EventDeclaration(string EventCode, string Description);
