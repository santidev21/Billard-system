using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class RecoveryRequest : Entity
{
    private RecoveryRequest()
    {
    }

    public RecoveryRequest(Guid tenantId, Guid userId, string codeHash, DateTimeOffset expiresAt)
    {
        TenantId = tenantId;
        UserId = userId;
        CodeHash = codeHash;
        ExpiresAt = expiresAt;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public Guid TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }
    public Guid UserId { get; private set; }
    public User? User { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public bool IsResolved => ResolvedAt.HasValue;

    public bool IsExpired() => DateTimeOffset.UtcNow >= ExpiresAt;

    public void Resolve()
    {
        ResolvedAt = DateTimeOffset.UtcNow;
    }
}
