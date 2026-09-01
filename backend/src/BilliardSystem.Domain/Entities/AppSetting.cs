using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class AppSetting : Entity
{
    private AppSetting()
    {
    }

    public AppSetting(string key, string value, Guid? tenantId = null)
    {
        Key = key;
        Value = value;
        TenantId = tenantId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public Guid? TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void Update(string value)
    {
        Value = value;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
