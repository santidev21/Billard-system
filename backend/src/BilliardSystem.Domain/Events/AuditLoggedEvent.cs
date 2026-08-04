using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Enums;

namespace BilliardSystem.Domain.Events;

public sealed record AuditLoggedEvent(Guid AuditLogId, AuditActionType ActionType) : DomainEvent;
