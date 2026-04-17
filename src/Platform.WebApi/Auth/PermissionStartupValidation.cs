using System.Reflection;
using System.Text;
using Platform.Core.Security;
using Platform.Module.Abstractions.Contracts;

namespace Platform.WebApi.Auth;

/// <summary>
/// 启动时校验：策略注册、平台权限常量、平台菜单、种子定义、业务模块权限声明是否四方一致。
/// </summary>
public static class PermissionStartupValidation
{
    public static void EnsureValidAtStartup(IReadOnlyCollection<IBusinessModule> businessModules)
    {
        var errors = new List<string>();
        CollectPlatformRegistryErrors(errors);
        CollectPlatformMenuErrors(errors);
        CollectSeedDefinitionErrors(errors);
        CollectBusinessModuleErrors(businessModules, errors);
        if (errors.Count == 0)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("权限一致性启动校验失败：");
        foreach (var line in errors)
        {
            sb.AppendLine($" - {line}");
        }

        throw new InvalidOperationException(sb.ToString().TrimEnd());
    }

    internal static HashSet<string> CollectModulePermissionCodes(IReadOnlyCollection<IBusinessModule> modules)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var module in modules)
        {
            foreach (var declaration in module.GetPermissions())
            {
                var code = declaration.PermissionCode?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                {
                    continue;
                }

                codes.Add(code);
            }
        }

        return codes;
    }

    internal static IReadOnlyList<string> MergeAdminSeedPermissionCodes(IReadOnlyCollection<IBusinessModule> modules)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in PlatformPermissionSeed.AdminRolePlatformPermissionCodes)
        {
            set.Add(code);
        }

        foreach (var code in CollectModulePermissionCodes(modules))
        {
            set.Add(code);
        }

        return set.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void CollectPlatformRegistryErrors(ICollection<string> errors)
    {
        var platformCodes = GetDeclaredStringConstants(typeof(PlatformPermissionCodes));
        var policyConstantValues = GetDeclaredStringConstants(typeof(PermissionPolicies));
        var bindings = PermissionCatalog.GetPolicyBindings();

        var boundPolicyNames = new HashSet<string>(bindings.Select(b => b.PolicyName), StringComparer.Ordinal);
        foreach (var kv in policyConstantValues)
        {
            var fieldName = kv.Key;
            var policyName = kv.Value;
            if (!boundPolicyNames.Contains(policyName))
            {
                errors.Add(
                    $"鉴权策略常量 {fieldName} 对应的策略名「{policyName}」未在 PermissionCatalog 的 PolicyBindings 中注册。");
            }
        }

        var codesInBindings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var binding in bindings)
        {
            if (!platformCodes.Values.Contains(binding.PermissionCode))
            {
                errors.Add(
                    $"策略「{binding.PolicyName}」绑定的权限码「{binding.PermissionCode}」不在 {nameof(PlatformPermissionCodes)} 中。");
            }

            if (!codesInBindings.Add(binding.PermissionCode))
            {
                errors.Add($"PolicyBindings 中权限码「{binding.PermissionCode}」重复绑定。");
            }
        }

        foreach (var code in platformCodes.Values)
        {
            if (!codesInBindings.Contains(code))
            {
                errors.Add($"{nameof(PlatformPermissionCodes)} 中的权限码「{code}」未出现在 PolicyBindings 中。");
            }
        }

        if (bindings.Length != platformCodes.Count)
        {
            errors.Add(
                $"PolicyBindings 数量 ({bindings.Length}) 与平台权限码数量 ({platformCodes.Count}) 不一致。");
        }

        if (policyConstantValues.Count != bindings.Length)
        {
            errors.Add(
                $"{nameof(PermissionPolicies)} 常量数量 ({policyConstantValues.Count}) 与 PolicyBindings ({bindings.Length}) 不一致。");
        }
    }

    private static void CollectPlatformMenuErrors(ICollection<string> errors)
    {
        var platformCodes = new HashSet<string>(GetDeclaredStringConstants(typeof(PlatformPermissionCodes)).Values, StringComparer.OrdinalIgnoreCase);
        foreach (var menu in PermissionCatalog.GetPlatformMenus())
        {
            var code = menu.PermissionCode?.Trim();
            if (string.IsNullOrEmpty(code))
            {
                continue;
            }

            if (!platformCodes.Contains(code))
            {
                errors.Add($"平台菜单「{menu.Key}」绑定的权限码「{code}」不是有效的平台权限码（模块权限不得绑定在平台壳菜单上）。");
            }
        }
    }

    private static void CollectSeedDefinitionErrors(ICollection<string> errors)
    {
        var platformCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in GetDeclaredStringConstants(typeof(PlatformPermissionCodes)).Values)
        {
            platformCodes.Add(c);
        }

        var adminSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in PlatformPermissionSeed.AdminRolePlatformPermissionCodes)
        {
            if (!adminSet.Add(c))
            {
                errors.Add($"{nameof(PlatformPermissionSeed.AdminRolePlatformPermissionCodes)} 中权限码「{c}」重复。");
            }

            if (!platformCodes.Contains(c))
            {
                errors.Add($"种子管理员权限列表包含非平台权限码：{c}");
            }
        }

        foreach (var code in platformCodes)
        {
            if (!adminSet.Contains(code))
            {
                errors.Add($"{nameof(PlatformPermissionSeed.AdminRolePlatformPermissionCodes)} 缺少平台权限码「{code}」。");
            }
        }

        foreach (var c in PlatformPermissionSeed.OpsRolePlatformPermissionCodes)
        {
            if (!platformCodes.Contains(c))
            {
                errors.Add($"种子运维角色权限「{c}」不是有效的平台权限码。");
            }
        }
    }

    private static void CollectBusinessModuleErrors(IReadOnlyCollection<IBusinessModule> modules, ICollection<string> errors)
    {
        var platformCodes = new HashSet<string>(GetDeclaredStringConstants(typeof(PlatformPermissionCodes)).Values, StringComparer.OrdinalIgnoreCase);
        var seenModuleCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var globalPermissionCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var module in modules.OrderBy(m => m.Metadata.ModuleCode, StringComparer.OrdinalIgnoreCase))
        {
            var moduleCode = module.Metadata.ModuleCode?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(moduleCode))
            {
                errors.Add("存在未声明 moduleCode 的业务模块实例。");
                continue;
            }

            if (!seenModuleCodes.Add(moduleCode))
            {
                errors.Add($"业务模块 moduleCode 重复：{moduleCode}");
            }

            var permissionDeclarations = module.GetPermissions().ToArray();
            var permissionCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in permissionDeclarations)
            {
                var code = p.PermissionCode?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(code))
                {
                    errors.Add($"[{moduleCode}] 存在空的权限码声明。");
                    continue;
                }

                if (platformCodes.Contains(code))
                {
                    errors.Add($"[{moduleCode}] 模块声明了与平台冲突的权限码「{code}」（业务权限码不得与平台权限码重复）。");
                }

                if (!permissionCodes.Add(code))
                {
                    errors.Add($"[{moduleCode}] 权限码「{code}」重复声明。");
                }

                if (globalPermissionCodes.TryAdd(code, moduleCode))
                {
                    continue;
                }

                errors.Add(
                    $"权限码「{code}」被多个模块声明：{globalPermissionCodes[code]} 与 {moduleCode}。");
            }

            foreach (var menu in module.GetMenus())
            {
                var menuPermission = menu.PermissionCode?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(menuPermission))
                {
                    errors.Add($"[{moduleCode}] 菜单「{menu.MenuCode}」未声明 permissionCode。");
                    continue;
                }

                if (!permissionCodes.Contains(menuPermission))
                {
                    errors.Add($"[{moduleCode}] 菜单「{menu.MenuCode}」绑定权限「{menuPermission}」，但该码未在本模块 GetPermissions 中声明。");
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, string> GetDeclaredStringConstants(Type type)
    {
        var dict = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.FieldType != typeof(string))
            {
                continue;
            }

            if (!field.IsLiteral)
            {
                continue;
            }

            var raw = field.GetRawConstantValue();
            if (raw is not string value || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            dict[field.Name] = value;
        }

        return dict;
    }
}
