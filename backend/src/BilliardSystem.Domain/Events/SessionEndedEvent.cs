using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Events;

public sealed record SessionEndedEvent(Guid TableId, Guid MatchId, Guid? ClosedByUserId) : DomainEvent;
