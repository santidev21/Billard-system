using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class AdminSession : Entity
{
    private const int LifetimeDays = 30;

    private AdminSession()
    {
    }

    public AdminSession(string tokenHash, DateTimeOffset expiresAt)
    {
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
        LastUsedAt = DateTimeOffset.UtcNow;
    }

    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset LastUsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public bool IsRevoked { get; private set; }

    public void Touch()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
        var newExpiry = LastUsedAt.AddDays(LifetimeDays);
        if (newExpiry > ExpiresAt)
        {
            ExpiresAt = newExpiry;
        }
    }

    public void Revoke() => IsRevoked = true;

    public bool IsValid() => !IsRevoked && DateTimeOffset.UtcNow < ExpiresAt;
}
