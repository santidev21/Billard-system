using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class AppSetting : Entity
{
    private AppSetting()
    {
    }

    public AppSetting(string key, string value)
    {
        Key = key;
        Value = value;
    }

    public string Key { get; private set; } = string.Empty;
    public string Value { get; private set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
