namespace BilliardSystem.Domain.Enums;

public enum BilliardTableStatus
{
    Available = 1,
    Occupied = 2,
    WaitingForWaiter = 3,
    WaitingForCheck = 4,
    OutOfService = 5
}
