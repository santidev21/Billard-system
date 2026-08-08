using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Enums;
using BilliardSystem.Domain.Events;

namespace BilliardSystem.Domain.Entities;

public sealed class BilliardTable : Entity
{
    private BilliardTable()
    {
    }

    public BilliardTable(string name, decimal hourlyRate)
    {
        Name = name;
        HourlyRate = hourlyRate;
    }

    public string Name { get; private set; } = string.Empty;
    public BilliardTableStatus Status { get; private set; } = BilliardTableStatus.Available;
    public decimal HourlyRate { get; private set; }
    public Guid? ActiveMatchId { get; private set; }

    public void SetHourlyRate(decimal hourlyRate) => HourlyRate = hourlyRate;

    public void Rename(string name) => Name = name;

    public void StartSession(Guid matchId, string whitePlayerName, string yellowPlayerName, Guid? employeeId)
    {
        if (Status != BilliardTableStatus.Available)
        {
            throw new InvalidOperationException("Only available tables can start a session.");
        }

        ActiveMatchId = matchId;
        Status = BilliardTableStatus.Occupied;
        AddDomainEvent(new SessionStartedEvent(Id, matchId, whitePlayerName, yellowPlayerName, employeeId));
    }

    public void MarkWaiterRequested(Guid matchId) =>
        SetStatusAndEvent(BilliardTableStatus.WaitingForWaiter, new WaiterRequestedEvent(Id, matchId));

    public void MarkCheckRequested(Guid matchId) =>
        SetStatusAndEvent(BilliardTableStatus.WaitingForCheck, new CheckRequestedEvent(Id, matchId));

    public void EndSession(Guid matchId, Guid? closedByUserId)
    {
        if (ActiveMatchId != matchId)
        {
            throw new InvalidOperationException("The match is not active on this table.");
        }

        ActiveMatchId = null;
        Status = BilliardTableStatus.Available;
        AddDomainEvent(new SessionEndedEvent(Id, matchId, closedByUserId));
    }

    private void SetStatusAndEvent(BilliardTableStatus status, IDomainEvent domainEvent)
    {
        Status = status;
        AddDomainEvent(domainEvent);
    }
}
