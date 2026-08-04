using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Events;

public sealed record CheckRequestedEvent(Guid TableId, Guid MatchId) : DomainEvent;
