using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Enums;

namespace BilliardSystem.Domain.Entities;

public sealed class AuditLog : Entity
{
    private AuditLog()
    {
    }

    public AuditLog(AuditActionType actionType, string description, Guid? userId, Guid? tableId, Guid? matchId, Guid? transactionId, Guid? tenantId = null)
    {
        ActionType = actionType;
        Description = description;
        UserId = userId;
        TableId = tableId;
        MatchId = matchId;
        TransactionId = transactionId;
        TenantId = tenantId;
    }

    public AuditActionType ActionType { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Guid? UserId { get; private set; }
    public Guid? TableId { get; private set; }
    public Guid? MatchId { get; private set; }
    public Guid? TransactionId { get; private set; }
    public Guid? TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
