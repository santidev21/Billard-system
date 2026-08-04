using Microsoft.AspNetCore.SignalR;

namespace BilliardSystem.API.Hubs;

public sealed class TableHub : Hub
{
    public Task JoinTableGroup(Guid tableId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"table:{tableId}");

    public Task LeaveTableGroup(Guid tableId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"table:{tableId}");
}
