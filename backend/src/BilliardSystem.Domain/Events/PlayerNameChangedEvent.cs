using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Events;

public sealed record PlayerNameChangedEvent(
    Guid TableId,
    Guid MatchId,
    string PlayerColor,
    string NewName,
    Guid? UserId) : DomainEvent;
