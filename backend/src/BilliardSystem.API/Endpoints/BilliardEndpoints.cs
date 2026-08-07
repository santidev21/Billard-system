using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BilliardSystem.API.Hubs;

namespace BilliardSystem.API.Endpoints;

public static class BilliardEndpoints
{
    public static IEndpointRouteBuilder MapBilliardEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BilliardSystem.API" }));

        api.MapGet("/tables", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var tables = await dbContext.Tables
                .AsNoTracking()
                .OrderBy(table => table.Name)
                .Select(table => new TableResponse(
                    table.Id,
                    table.Name,
                    table.Status.ToString(),
                    table.HourlyRate,
                    table.ActiveMatchId))
                .ToListAsync(cancellationToken);

            return Results.Ok(tables);
        });

        api.MapGet("/tables/{id}", async (Guid id, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables
                .AsNoTracking()
                .FirstOrDefaultAsync(table => table.Id == id, cancellationToken);

            if (table is null)
            {
                return Results.NotFound();
            }

            var match = table.ActiveMatchId is { } matchId
                ? await dbContext.MatchHistories
                    .AsNoTracking()
                    .Include(history => history.Consumptions)
                    .FirstOrDefaultAsync(history => history.Id == matchId, cancellationToken)
                : null;

            return Results.Ok(new TableDetailResponse(
                table.Id,
                table.Name,
                table.Status.ToString(),
                table.HourlyRate,
                table.ActiveMatchId,
                match is null ? null : ToMatchDetailResponse(match)));
        });

        api.MapGet("/products", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var categories = await dbContext.Categories
                .AsNoTracking()
                .Where(category => category.IsActive)
                .OrderBy(category => category.SortOrder)
                .ThenBy(category => category.Name)
                .Select(category => new ProductCategoryResponse(
                    category.Id,
                    category.Name,
                    category.Products
                        .Where(product => product.IsActive)
                        .OrderBy(product => product.Name)
                        .Select(product => new ProductResponse(product.Id, product.Name, product.Price))
                        .ToArray()))
                .ToListAsync(cancellationToken);

            return Results.Ok(categories);
        });

        api.MapPost("/products", async (
            CreateProductRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var category = await dbContext.Categories.FirstOrDefaultAsync(c => c.Id == request.CategoryId && c.IsActive, cancellationToken);
            if (category is null)
            {
                return Results.BadRequest("Categoría inexistente.");
            }

            var product = new Product(request.CategoryId, request.Name, request.Price);
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new ProductResponse(product.Id, product.Name, product.Price));
        });

        api.MapPut("/products/{id}", async (
            Guid id,
            UpdateProductRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (product is null)
            {
                return Results.NotFound();
            }

            product.Update(request.Name, request.Price);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        });

        api.MapDelete("/products/{id}", async (Guid id, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            if (product is null)
            {
                return Results.NotFound();
            }

            product.Deactivate();
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        });

        api.MapGet("/settings", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var settings = await dbContext.Settings
                .AsNoTracking()
                .OrderBy(setting => setting.Key)
                .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

            return Results.Ok(settings);
        });

        api.MapPut("/settings", async (Dictionary<string, string> values, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            foreach (var pair in values)
            {
                var setting = await dbContext.Settings.FirstOrDefaultAsync(s => s.Key == pair.Key, cancellationToken);
                if (setting is null)
                {
                    dbContext.Settings.Add(new AppSetting(pair.Key, pair.Value));
                }
                else
                {
                    setting.Update(pair.Value);
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        });

        api.MapPost("/tables/{id}/start", async (
            Guid id,
            StartSessionRequest request,
            BilliardDbContext dbContext,
            IHubContext<TableHub> hub,
            CancellationToken cancellationToken) =>
        {
            if (await IsIdempotentAsync(dbContext, request.TransactionId, cancellationToken))
            {
                return Results.Ok();
            }

            var table = await dbContext.Tables.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            var match = new MatchHistory(
                table.Id,
                request.WhitePlayerName,
                request.YellowPlayerName,
                table.HourlyRate,
                openedByUserId: null,
                gameMode: request.GameMode);

            table.StartSession(match.Id, request.WhitePlayerName, request.YellowPlayerName, employeeId: null);

            dbContext.MatchHistories.Add(match);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.SessionStarted, request.UserId, table.Id, match.Id, request.TransactionId,
                $"Inicio de partida en {table.Name} (modo {request.GameMode})", cancellationToken);

            await hub.Clients.All.SendAsync("TableStateUpdated", new { tableId = table.Id, status = "Occupied" }, cancellationToken);
            await hub.Clients.Group($"table:{table.Id}").SendAsync("SessionStarted", new { tableId = table.Id, matchId = match.Id }, cancellationToken);
            return Results.Ok(new StartSessionResponse(table.Id, match.Id));
        });

        api.MapPost("/tables/{id}/score", async (
            Guid id,
            ScoreRequest request,
            BilliardDbContext dbContext,
            IHubContext<TableHub> hub,
            CancellationToken cancellationToken) =>
        {
            if (await IsIdempotentAsync(dbContext, request.TransactionId, cancellationToken))
            {
                return Results.Ok();
            }

            var table = await dbContext.Tables.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
            if (table?.ActiveMatchId is not { } matchId)
            {
                return Results.BadRequest("No hay partida activa en esta mesa.");
            }

            var match = await dbContext.MatchHistories.FirstOrDefaultAsync(history => history.Id == matchId, cancellationToken);
            if (match is null)
            {
                return Results.NotFound();
            }

            var scoreLog = match.AddScore(request.PlayerColor, request.Delta, request.UserId);
            dbContext.MatchScoreLogs.Add(scoreLog);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.PlayerScored, request.UserId, table.Id, match.Id, request.TransactionId,
                $"Carambola {request.PlayerColor} {request.Delta:+0;-0;0} -> {scoreLog.ResultingScore}", cancellationToken);

            await hub.Clients.Group($"table:{id}").SendAsync("PlayerScored", new
            {
                tableId = id,
                playerColor = request.PlayerColor,
                delta = request.Delta,
                newScore = scoreLog.ResultingScore,
                totalCarambolas = match.TotalCarambolas
            }, cancellationToken);

            return Results.Ok(new ScoreResponse(scoreLog.ResultingScore));
        });

        api.MapPost("/tables/{id}/players", async (
            Guid id,
            RenamePlayersRequest request,
            BilliardDbContext dbContext,
            IHubContext<TableHub> hub,
            CancellationToken cancellationToken) =>
        {
            if (await IsIdempotentAsync(dbContext, request.TransactionId, cancellationToken))
            {
                return Results.Ok();
            }

            var table = await dbContext.Tables.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
            if (table?.ActiveMatchId is not { } matchId)
            {
                return Results.NotFound();
            }

            var match = await dbContext.MatchHistories.FirstOrDefaultAsync(history => history.Id == matchId, cancellationToken);
            if (match is null)
            {
                return Results.NotFound();
            }

            match.RenamePlayer("white", request.WhitePlayerName);
            match.RenamePlayer("yellow", request.YellowPlayerName);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.PlayerNameChanged, request.UserId, table.Id, match.Id, request.TransactionId,
                "Nombres de jugadores actualizados", cancellationToken);

            await hub.Clients.Group($"table:{id}").SendAsync("PlayerNamesChanged", new
            {
                tableId = id,
                whitePlayerName = request.WhitePlayerName,
                yellowPlayerName = request.YellowPlayerName
            }, cancellationToken);

            return Results.Ok();
        });

        api.MapPost("/tables/{id}/consumption", async (
            Guid id,
            AddConsumptionRequest request,
            BilliardDbContext dbContext,
            IHubContext<TableHub> hub,
            CancellationToken cancellationToken) =>
        {
            if (await IsIdempotentAsync(dbContext, request.TransactionId, cancellationToken))
            {
                return Results.Ok();
            }

            var table = await dbContext.Tables.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
            var product = await dbContext.Products.FirstOrDefaultAsync(product => product.Id == request.ProductId, cancellationToken);
            if (table?.ActiveMatchId is not { } matchId || product is null)
            {
                return Results.BadRequest("Partida o producto inexistente.");
            }

            var match = await dbContext.MatchHistories.FirstOrDefaultAsync(history => history.Id == matchId, cancellationToken);
            if (match is null)
            {
                return Results.NotFound();
            }

            var consumption = match.AddConsumption(product.Id, product.Name, product.Price, request.Quantity);
            dbContext.MatchConsumptions.Add(consumption);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.ConsumptionAdded, request.UserId, table.Id, match.Id, request.TransactionId,
                $"{request.Quantity} x {product.Name}", cancellationToken);

            await hub.Clients.Group($"table:{id}").SendAsync("ConsumptionAdded", new
            {
                tableId = id,
                item = new ConsumptionAmountResponse(consumption.Id, product.Name, product.Price, request.Quantity, product.Price * request.Quantity),
                consumptionTotal = match.ConsumptionTotal
            }, cancellationToken);

            return Results.Ok(new ConsumptionAddedResponse(match.ConsumptionTotal));
        });

        api.MapPost("/tables/{id}/call-waiter", async (
            Guid id,
            TableRequest? request,
            BilliardDbContext dbContext,
            IHubContext<TableHub> hub,
            CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (table.ActiveMatchId is { } matchId)
            {
                table.MarkWaiterRequested(matchId);
                await dbContext.SaveChangesAsync(cancellationToken);
                await WriteAuditAsync(dbContext, AuditActionType.WaiterRequested, request?.UserId, table.Id, matchId, request?.TransactionId,
                    "Llamada de mesero", cancellationToken);
            }

            await hub.Clients.All.SendAsync("AdminNotification", new { type = "waiter", tableId = id, tableName = table.Name, timestamp = DateTimeOffset.UtcNow }, cancellationToken);
            return Results.Ok();
        });

        api.MapPost("/tables/{id}/request-check", async (
            Guid id,
            TableRequest? request,
            BilliardDbContext dbContext,
            IHubContext<TableHub> hub,
            CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            var consumptionTotal = 0m;
            Guid? matchId = table.ActiveMatchId;
            if (matchId is { } activeMatch)
            {
                consumptionTotal = await dbContext.MatchHistories
                    .Where(history => history.Id == activeMatch)
                    .Select(history => history.ConsumptionTotal)
                    .FirstOrDefaultAsync(cancellationToken);

                table.MarkCheckRequested(activeMatch);
                await dbContext.SaveChangesAsync(cancellationToken);
                await WriteAuditAsync(dbContext, AuditActionType.CheckRequested, request?.UserId, table.Id, activeMatch, request?.TransactionId,
                    "Solicitud de cuenta", cancellationToken);
            }

            await hub.Clients.All.SendAsync("AdminRequest", new { type = "check", tableId = id, tableName = table.Name, total = consumptionTotal, timestamp = DateTimeOffset.UtcNow }, cancellationToken);
            return Results.Ok();
        });

        api.MapPost("/tables/{id}/finish", async (
            Guid id,
            FinishSessionRequest request,
            BilliardDbContext dbContext,
            IHubContext<TableHub> hub,
            CancellationToken cancellationToken) =>
        {
            if (await IsIdempotentAsync(dbContext, request.TransactionId, cancellationToken))
            {
                return Results.Ok();
            }

            var table = await dbContext.Tables.FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (table.ActiveMatchId is not { } matchId)
            {
                return Results.BadRequest("No hay partida activa.");
            }

            var match = await dbContext.MatchHistories
                .Include(history => history.Consumptions)
                .FirstOrDefaultAsync(history => history.Id == matchId, cancellationToken);
            if (match is null)
            {
                return Results.NotFound();
            }

            var endedAt = DateTimeOffset.UtcNow;
            var elapsedSeconds = Math.Max(0, (int)(endedAt - match.StartedAt).TotalSeconds);
            var tableTotal = Math.Round((elapsedSeconds / 3600m) * match.HourlyRateSnapshot, 2);
            match.Close(endedAt, tableTotal, match.ConsumptionTotal, request.ClosedByUserId);
            table.EndSession(match.Id, request.ClosedByUserId);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.SessionEnded, request.ClosedByUserId, table.Id, match.Id, request.TransactionId,
                $"Cierre de partida en {table.Name}, total {match.GrandTotal:C}", cancellationToken);

            await hub.Clients.Group($"table:{id}").SendAsync("SessionEnded", new
            {
                tableId = id,
                matchHistoryId = match.Id,
                grandTotal = match.GrandTotal,
                winnerName = match.WhiteScore >= match.YellowScore ? match.WhitePlayerName : match.YellowPlayerName
            }, cancellationToken);

            await hub.Clients.All.SendAsync("TableStateUpdated", new { tableId = id, status = "Available" }, cancellationToken);
            return Results.Ok(new FinishSessionResponse(match.Id, match.GrandTotal));
        });

        api.MapGet("/matches", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var matches = await dbContext.MatchHistories
                .AsNoTracking()
                .OrderByDescending(history => history.StartedAt)
                .Select(history => new MatchListItemResponse(
                    history.Id,
                    history.TableId,
                    history.WhitePlayerName,
                    history.YellowPlayerName,
                    history.WhiteScore,
                    history.YellowScore,
                    history.TotalCarambolas,
                    history.GameMode.ToString(),
                    history.StartedAt,
                    history.EndedAt,
                    history.GrandTotal))
                .ToListAsync(cancellationToken);

            return Results.Ok(matches);
        });

        api.MapGet("/matches/{id}", async (Guid id, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var match = await dbContext.MatchHistories
                .AsNoTracking()
                .Include(history => history.ScoreLogs)
                .Include(history => history.Consumptions)
                .FirstOrDefaultAsync(history => history.Id == id, cancellationToken);

            return match is null ? Results.NotFound() : Results.Ok(ToMatchDetailResponse(match));
        });

        api.MapGet("/dashboard/summary", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var today = DateTimeOffset.UtcNow.Date;
            var tables = await dbContext.Tables.AsNoTracking().ToListAsync(cancellationToken);
            var salesToday = await dbContext.MatchHistories
                .AsNoTracking()
                .Where(match => match.EndedAt != null && match.EndedAt >= today)
                .SumAsync(match => match.GrandTotal, cancellationToken);

            return Results.Ok(new DashboardSummaryResponse(
                TotalTables: tables.Count,
                AvailableTables: tables.Count(table => table.Status == BilliardTableStatus.Available),
                OccupiedTables: tables.Count(table => table.Status != BilliardTableStatus.Available),
                SalesToday: salesToday));
        });

        api.MapGet("/dashboard/top-products", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var start = DateTimeOffset.UtcNow.Date.AddDays(-7);
            var items = await dbContext.MatchConsumptions
                .AsNoTracking()
                .Where(item => item.CreatedAt >= start)
                .GroupBy(item => item.ProductNameSnapshot)
                .Select(group => new TopProductResponse(group.Key, group.Sum(item => item.Quantity), group.Sum(item => item.Total)))
                .OrderByDescending(group => group.Quantity)
                .Take(10)
                .ToListAsync(cancellationToken);

            return Results.Ok(items);
        });

        api.MapGet("/audit/logs", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var logs = await dbContext.AuditLogs
                .AsNoTracking()
                .OrderByDescending(log => log.CreatedAt)
                .Take(200)
                .Select(log => new AuditLogResponse(log.Id, log.ActionType.ToString(), log.Description, log.UserId, log.TableId, log.MatchId, log.TransactionId, log.CreatedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(logs);
        });

        return app;
    }

    private static MatchDetailResponse ToMatchDetailResponse(MatchHistory match) => new(
        match.Id,
        match.WhitePlayerName,
        match.YellowPlayerName,
        match.WhiteScore,
        match.YellowScore,
        match.GameMode.ToString(),
        match.StartedAt,
        match.EndedAt is { } endedAt ? endedAt - match.StartedAt : TimeSpan.Zero,
        match.ConsumptionTotal,
        match.Consumptions.Select(consumption => new ConsumptionAmountResponse(
            consumption.Id, consumption.ProductNameSnapshot, consumption.UnitPriceSnapshot, consumption.Quantity, consumption.Total)).ToArray());

    private static async Task<bool> IsIdempotentAsync(BilliardDbContext dbContext, Guid? transactionId, CancellationToken cancellationToken)
    {
        if (transactionId is null)
        {
            return false;
        }

        return await dbContext.AuditLogs.AnyAsync(log => log.TransactionId == transactionId, cancellationToken);
    }

    private static async Task WriteAuditAsync(
        BilliardDbContext dbContext,
        AuditActionType actionType,
        Guid? userId,
        Guid? tableId,
        Guid? matchId,
        Guid? transactionId,
        string description,
        CancellationToken cancellationToken)
    {
        dbContext.AuditLogs.Add(new AuditLog(actionType, description, userId, tableId, matchId, transactionId));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

#region Request / Response DTOs

public sealed record TableResponse(Guid Id, string Name, string Status, decimal HourlyRate, Guid? ActiveMatchId);
public sealed record ProductCategoryResponse(Guid Id, string Name, IReadOnlyCollection<ProductResponse> Products);
public sealed record ProductResponse(Guid Id, string Name, decimal Price);
public sealed record DashboardSummaryResponse(int TotalTables, int AvailableTables, int OccupiedTables, decimal SalesToday);

public sealed record MatchDetailResponse(
    Guid Id,
    string WhitePlayerName,
    string YellowPlayerName,
    int WhiteScore,
    int YellowScore,
    string GameMode,
    DateTimeOffset StartedAt,
    TimeSpan Elapsed,
    decimal ConsumptionTotal,
    IReadOnlyCollection<ConsumptionAmountResponse> Consumptions);

public sealed record TableDetailResponse(
    Guid Id,
    string Name,
    string Status,
    decimal HourlyRate,
    Guid? ActiveMatchId,
    MatchDetailResponse? ActiveMatch);

public sealed record StartSessionRequest(string WhitePlayerName, string YellowPlayerName, GameMode GameMode, Guid? TransactionId, Guid? UserId);
public sealed record ScoreRequest(string PlayerColor, int Delta, Guid? TransactionId, Guid? UserId);
public sealed record RenamePlayersRequest(string WhitePlayerName, string YellowPlayerName, Guid? TransactionId, Guid? UserId);
public sealed record AddConsumptionRequest(Guid ProductId, int Quantity, Guid? TransactionId, Guid? UserId);
public sealed record CreateProductRequest(Guid CategoryId, string Name, decimal Price);
public sealed record UpdateProductRequest(string Name, decimal Price);
public sealed record TableRequest(Guid? TransactionId, Guid? UserId);
public sealed record FinishSessionRequest(Guid? TransactionId, Guid? ClosedByUserId);
public sealed record StartSessionResponse(Guid TableId, Guid MatchId);
public sealed record ScoreResponse(int NewScore);
public sealed record ConsumptionAddedResponse(decimal ConsumptionTotal);
public sealed record FinishSessionResponse(Guid MatchHistoryId, decimal GrandTotal);

public sealed record ConsumptionAmountResponse(Guid Id, string ProductName, decimal UnitPrice, int Quantity, decimal Total);

public sealed record MatchListItemResponse(
    Guid Id,
    Guid TableId,
    string WhitePlayerName,
    string YellowPlayerName,
    int WhiteScore,
    int YellowScore,
    int TotalCarambolas,
    string GameMode,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    decimal GrandTotal);

public sealed record TopProductResponse(string Name, int Quantity, decimal Total);

public sealed record AuditLogResponse(
    Guid Id,
    string ActionType,
    string Description,
    Guid? UserId,
    Guid? TableId,
    Guid? MatchId,
    Guid? TransactionId,
    DateTimeOffset CreatedAt);

#endregion