using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Events;

public sealed record ReplayRequestedEvent(Guid TableId, Guid MatchId, int SecondsRequested) : DomainEvent;
