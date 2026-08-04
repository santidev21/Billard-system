using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Events;

public sealed record PlayerScoredEvent(
    Guid TableId,
    Guid MatchId,
    string PlayerColor,
    int Delta,
    int ResultingScore,
    Guid? UserId) : DomainEvent;
