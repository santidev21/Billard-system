using System.Security.Claims;

namespace BilliardSystem.API.Auth;

public static class TenantContext
{
    public static Guid? GetTenantId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("tenant");
        return claim?.Value is { Length: > 0 } str && Guid.TryParse(str, out var id) ? id : null;
    }

    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
        return claim?.Value is { Length: > 0 } str && Guid.TryParse(str, out var id) ? id : Guid.Empty;
    }

    public static string GetUserName(this ClaimsPrincipal user)
    {
        return user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("unique_name")?.Value ?? string.Empty;
    }

    public static bool IsSuperAdmin(this ClaimsPrincipal user)
    {
        var role = user.FindFirst(ClaimTypes.Role)?.Value ?? user.FindFirst("role")?.Value;
        return string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
    }
}
