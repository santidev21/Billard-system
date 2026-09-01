using System.Security.Cryptography;
using System.Text;
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

    public async Task JoinAdminGroup(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        var tokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
        var scope = Context.GetHttpContext()!.RequestServices.CreateScope();
        try
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<BilliardDbContext>();

            var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.TokenHash == tokenHash);
            if (session is not null && session.IsValid())
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
            }
        }
        finally
        {
            (scope as IDisposable)?.Dispose();
        }
    }

    public Task LeaveAdminGroup() =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, "admins");
}
