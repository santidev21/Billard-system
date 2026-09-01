using System.Security.Claims;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BilliardSystem.API.Hubs;

public sealed class TableHub : Hub
{
    public Task JoinTableGroup(Guid tableId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"table:{tableId}");

    public Task LeaveTableGroup(Guid tableId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"table:{tableId}");

    public Task JoinAdminGroup()
    {
        var tenantId = Context.User?.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            return Task.CompletedTask;
        }
        return Groups.AddToGroupAsync(Context.ConnectionId, $"admins:{tenantId}");
    }

    public Task LeaveAdminGroup()
    {
        var tenantId = Context.User?.FindFirst("tenant")?.Value;
        if (string.IsNullOrEmpty(tenantId))
        {
            return Task.CompletedTask;
        }
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, $"admins:{tenantId}");
    }
}
