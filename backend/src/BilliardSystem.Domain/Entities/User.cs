using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Enums;

namespace BilliardSystem.Domain.Entities;

public sealed class User : Entity
{
    private User()
    {
    }

    public User(string displayName, string userName, string passwordHash, UserRole role, Guid? tenantId = null)
    {
        DisplayName = displayName;
        UserName = userName;
        PasswordHash = passwordHash;
        Role = role;
        TenantId = tenantId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string DisplayName { get; private set; } = string.Empty;
    public string UserName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid? TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }
    public string? Email { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    public void SetEmail(string? email) => Email = email;
    public void SetTenantId(Guid tenantId) => TenantId = tenantId;
    public void SetPassword(string passwordHash) => PasswordHash = passwordHash;
}
