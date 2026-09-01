using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Enums;
using BilliardSystem.Domain.Events;

namespace BilliardSystem.Domain.Entities;

public sealed class BilliardTable : Entity
{
    private BilliardTable()
    {
    }

    public BilliardTable(string name, decimal hourlyRate, Guid tenantId, string? code = null)
    {
        Name = name;
        HourlyRate = hourlyRate;
        TenantId = tenantId;
        Code = code ?? string.Empty;
    }

    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public BilliardTableStatus Status { get; private set; } = BilliardTableStatus.Available;
    public decimal HourlyRate { get; private set; }
    public Guid? ActiveMatchId { get; private set; }
    public Guid TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }

    public void SetHourlyRate(decimal hourlyRate) => HourlyRate = hourlyRate;

    public void Rename(string name) => Name = name;

    public void SetCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("El código de la mesa no puede estar vacío.");
        }

        Code = code.Trim().ToUpperInvariant();
    }

    public void Disable()
    {
        if (ActiveMatchId is not null)
        {
            throw new InvalidOperationException("No se puede inhabilitar una mesa con partida activa.");
        }

        IsActive = false;
        Status = BilliardTableStatus.OutOfService;
    }

    public void Enable()
    {
        IsActive = true;
        Status = ActiveMatchId is null ? BilliardTableStatus.Available : BilliardTableStatus.Occupied;
    }

    public void MarkAttended()
    {
        if (Status is not (BilliardTableStatus.WaitingForWaiter or BilliardTableStatus.WaitingForCheck))
        {
            return;
        }

        Status = BilliardTableStatus.Occupied;
    }

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
