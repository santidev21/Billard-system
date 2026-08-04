using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Events;

public sealed record WaiterRequestedEvent(Guid TableId, Guid MatchId) : DomainEvent;
