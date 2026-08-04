using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Events;

public sealed record SessionStartedEvent(
    Guid TableId,
    Guid MatchId,
    string WhitePlayerName,
    string YellowPlayerName,
    Guid? EmployeeId) : DomainEvent;
