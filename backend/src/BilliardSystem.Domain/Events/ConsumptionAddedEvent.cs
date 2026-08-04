using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Events;

public sealed record ConsumptionAddedEvent(
    Guid TableId,
    Guid MatchId,
    Guid ProductId,
    int Quantity,
    Guid? UserId) : DomainEvent;
