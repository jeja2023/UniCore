using Platform.Core.Abstractions;
using System.Security.Claims;

namespace Platform.WebApi.Auth;

public sealed class HttpTenantContextAccessor(IHttpContextAccessor httpContextAccessor) : ITenantContextAccessor
{
    public string TenantId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            var tenantId = context?.User.FindFirstValue("tenant_id")
                           ?? context?.Request.Headers["X-Tenant-Id"].ToString();
            return string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId.Trim();
        }
    }
}
