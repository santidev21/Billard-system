namespace BilliardSystem.Domain.Enums;

public enum AuditActionType
{
    SessionStarted = 1,
    PlayerScored = 2,
    PlayerNameChanged = 3,
    ConsumptionAdded = 4,
    WaiterRequested = 5,
    CheckRequested = 6,
    ReplayRequested = 7,
    SessionEnded = 8,
    SettingsChanged = 9,
    ProductChanged = 10,
    UserAuthenticated = 11
}
