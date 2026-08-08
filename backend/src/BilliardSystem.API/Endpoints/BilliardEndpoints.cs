using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BilliardSystem.API.Hubs;
using System.Security.Cryptography;
using System.Text;

namespace BilliardSystem.API.Endpoints;

public static class BilliardEndpoints
{
    public static IEndpointRouteBuilder MapBilliardEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BilliardSystem.API" }));

        api.MapPost("/auth/login", async (LoginRequest request, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { message = "Ingresa la clave de acceso." });
            }

            var adminPassword = await dbContext.Settings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "AdminPassword", cancellationToken);

            if (adminPassword is null || string.IsNullOrWhiteSpace(adminPassword.Value))
            {
                dbContext.Settings.Add(new AppSetting("AdminPassword", Hash(request.Password!)));
                await dbContext.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { token = Guid.NewGuid().ToString() });
            }

            return !string.Equals(adminPassword.Value, Hash(request.Password ?? string.Empty), StringComparison.Ordinal)
                ? Results.Unauthorized()
                : Results.Ok(new { token = Guid.NewGuid().ToString() });
        });

        api.MapPost("/auth/change-password", async (ChangePasswordRequest request, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 4)
            {
                return Results.BadRequest(new { message = "La nueva clave debe tener al menos 4 caracteres." });
            }

            var adminPassword = await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Key == "AdminPassword", cancellationToken);

            if (adminPassword is null || string.IsNullOrWhiteSpace(adminPassword.Value))
            {
                dbContext.Settings.Add(new AppSetting("AdminPassword", Hash(request.NewPassword!)));
            }
            else if (!string.Equals(adminPassword.Value, Hash(request.CurrentPassword ?? string.Empty), StringComparison.Ordinal))
            {
                return Results.Json(new { message = "La clave actual no coincide." }, statusCode: StatusCodes.Status401Unauthorized);
            }
            else
            {
                adminPassword.Update(Hash(request.NewPassword));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { ok = true });
        });

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

        api.MapPost("/tables", async (CreateTableRequest request, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return Results.BadRequest(new { message = "El nombre de la mesa es obligatorio." });
            }

            var rate = request.HourlyRate > 0 ? request.HourlyRate : await GetGlobalRateAsync(dbContext, cancellationToken);
            var table = new BilliardTable(request.Name.Trim(), rate);
            dbContext.Tables.Add(table);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Status.ToString(), table.HourlyRate, table.ActiveMatchId));
        });

        api.MapPut("/tables/{id}", async (Guid id, UpdateTableRequest request, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                table.Rename(request.Name.Trim());
            }

            if (request.HourlyRate > 0)
            {
                table.SetHourlyRate(request.HourlyRate);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Status.ToString(), table.HourlyRate, table.ActiveMatchId));
        });

        api.MapPut("/tables/rate/all", async (UpdateAllRatesRequest request, BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken cancellationToken) =>
        {
            if (request.HourlyRate <= 0)
            {
                return Results.BadRequest(new { message = "La tarifa debe ser mayor a cero." });
            }

            var tables = await dbContext.Tables.ToListAsync(cancellationToken);
            foreach (var table in tables)
            {
                table.SetHourlyRate(request.HourlyRate);
            }

            var rateSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.Key == "HourlyRate", cancellationToken);
            if (rateSetting is null)
            {
                dbContext.Settings.Add(new AppSetting("HourlyRate", request.HourlyRate.ToString()));
            }
            else
            {
                rateSetting.Update(request.HourlyRate.ToString());
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await hub.Clients.All.SendAsync("TableStateUpdated", new { tableId = (Guid?)null, status = "RateChanged" }, cancellationToken);
            return Results.Ok(new { updated = tables.Count });
        });

        api.MapGet("/products", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var products = await dbContext.Products
                .AsNoTracking()
                .Where(product => product.IsActive)
                .OrderBy(product => product.Name)
                .Select(product => new ProductResponse(product.Id, product.Name, product.Price))
                .ToListAsync(cancellationToken);

            return Results.Ok(products);
        });

        api.MapPost("/products", async (
            CreateProductRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Price <= 0)
            {
                return Results.BadRequest(new { message = "El nombre y el precio del producto son obligatorios." });
            }

            var category = await dbContext.Categories
                .Where(category => category.IsActive)
                .OrderBy(category => category.SortOrder)
                .FirstOrDefaultAsync(cancellationToken);

            if (category is null)
            {
                category = new ProductCategory("General");
                dbContext.Categories.Add(category);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var product = new Product(category.Id, request.Name.Trim(), request.Price);
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

            await hub.Clients.All.SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);

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

            await hub.Clients.All.SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);

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

            var match = await dbContext.MatchHistories
                .Include(history => history.Consumptions)
                .FirstOrDefaultAsync(history => history.Id == matchId, cancellationToken);
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
                item = new ConsumptionAmountResponse(consumption.Id, product.Name, product.Price, request.Quantity, product.Price * request.Quantity, consumption.CreatedAt),
                consumptionTotal = match.ConsumptionTotal
            }, cancellationToken);

            await hub.Clients.All.SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);

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
                tableTotal = match.TableTotal,
                consumptionTotal = match.ConsumptionTotal,
                grandTotal = match.GrandTotal,
                winnerName = match.WhiteScore >= match.YellowScore ? match.WhitePlayerName : match.YellowPlayerName
            }, cancellationToken);

            await hub.Clients.All.SendAsync("TableStateUpdated", new { tableId = id, status = "Available" }, cancellationToken);
            return Results.Ok(new FinishSessionResponse(match.Id, match.GrandTotal));
        });

        api.MapPost("/tables/{id}/finish-round", async (
            Guid id,
            TableRequest request,
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
                return Results.BadRequest("No hay partida activa.");
            }

            var match = await dbContext.MatchHistories.FirstOrDefaultAsync(history => history.Id == matchId, cancellationToken);
            if (match is null)
            {
                return Results.NotFound();
            }

            var round = match.CloseRound();
            dbContext.MatchRounds.Add(round);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.RoundCompleted, request.UserId, table.Id, match.Id, request.TransactionId,
                round.WinnerName is null ? $"Ronda {round.RoundNumber} en {table.Name}: empate {round.WhiteScore}-{round.YellowScore}"
                    : $"Ronda {round.RoundNumber} en {table.Name}: gana {round.WinnerName} {round.WhiteScore}-{round.YellowScore}", cancellationToken);

            await hub.Clients.Group($"table:{id}").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);
            await hub.Clients.All.SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);
            await hub.Clients.Group($"table:{id}").SendAsync("PlayerScored", new
            {
                tableId = id,
                playerColor = "white",
                delta = 0,
                newScore = 0,
                totalCarambolas = 0
            }, cancellationToken);

            return Results.Ok(new RoundResponse(round.Id, round.RoundNumber, round.WhiteScore, round.YellowScore, round.WinnerName));
        });

        api.MapGet("/tables/{id}/rounds", async (Guid id, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.AsNoTracking().FirstOrDefaultAsync(table => table.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (table.ActiveMatchId is not { } matchId)
            {
                return Results.Ok(new RoundHistoryResponse(0, 0, 0, []));
            }

            var match = await dbContext.MatchHistories
                .AsNoTracking()
                .Include(history => history.Rounds)
                .FirstOrDefaultAsync(history => history.Id == matchId, cancellationToken);
            if (match is null)
            {
                return Results.NotFound();
            }

            var rounds = match.Rounds
                .OrderBy(round => round.RoundNumber)
                .Select(round => new RoundDetailResponse(
                    round.RoundNumber,
                    round.WhiteScore,
                    round.YellowScore,
                    round.WinnerName,
                    round.EndedAt))
                .ToArray();

            var whiteRounds = rounds.Count(r => r.WinnerName == match.WhitePlayerName);
            var yellowRounds = rounds.Count(r => r.WinnerName == match.YellowPlayerName);

            return Results.Ok(new RoundHistoryResponse(whiteRounds, yellowRounds, match.RoundNumber, rounds));
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
            var endedToday = await dbContext.MatchHistories
                .AsNoTracking()
                .Where(match => match.EndedAt != null && match.EndedAt >= today)
                .ToListAsync(cancellationToken);

            var salesByGame = endedToday.Sum(match => match.TableTotal);
            var salesByConsumption = endedToday.Sum(match => match.ConsumptionTotal);

            return Results.Ok(new DashboardSummaryResponse(
                TotalTables: tables.Count,
                AvailableTables: tables.Count(table => table.Status == BilliardTableStatus.Available),
                OccupiedTables: tables.Count(table => table.Status != BilliardTableStatus.Available),
                SalesToday: salesByGame + salesByConsumption,
                SalesByGame: salesByGame,
                SalesByConsumption: salesByConsumption));
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
            consumption.Id, consumption.ProductNameSnapshot, consumption.UnitPriceSnapshot, consumption.Quantity, consumption.Total, consumption.CreatedAt)).ToArray());

    private static async Task<decimal> GetGlobalRateAsync(BilliardDbContext dbContext, CancellationToken cancellationToken)
    {
        var setting = await dbContext.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "HourlyRate", cancellationToken);

        if (setting is not null && decimal.TryParse(setting.Value, out var rate) && rate > 0)
        {
            return rate;
        }

        var anyTable = await dbContext.Tables.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return anyTable?.HourlyRate ?? 12000m;
    }

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

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}

#region Request / Response DTOs

public sealed record TableResponse(Guid Id, string Name, string Status, decimal HourlyRate, Guid? ActiveMatchId);
public sealed record ProductCategoryResponse(Guid Id, string Name, IReadOnlyCollection<ProductResponse> Products);
public sealed record ProductResponse(Guid Id, string Name, decimal Price);
public sealed record DashboardSummaryResponse(int TotalTables, int AvailableTables, int OccupiedTables, decimal SalesToday, decimal SalesByGame, decimal SalesByConsumption);

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
public sealed record LoginRequest(string? Password);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record CreateTableRequest(string Name, decimal HourlyRate);
public sealed record UpdateTableRequest(string? Name, decimal HourlyRate);
public sealed record UpdateAllRatesRequest(decimal HourlyRate);
public sealed record ScoreRequest(string PlayerColor, int Delta, Guid? TransactionId, Guid? UserId);
public sealed record RenamePlayersRequest(string WhitePlayerName, string YellowPlayerName, Guid? TransactionId, Guid? UserId);
public sealed record AddConsumptionRequest(Guid ProductId, int Quantity, Guid? TransactionId, Guid? UserId);
public sealed record CreateProductRequest(string Name, decimal Price);
public sealed record UpdateProductRequest(string Name, decimal Price);
public sealed record TableRequest(Guid? TransactionId, Guid? UserId);
public sealed record FinishSessionRequest(Guid? TransactionId, Guid? ClosedByUserId);
public sealed record StartSessionResponse(Guid TableId, Guid MatchId);public sealed record ScoreResponse(int NewScore);
public sealed record ConsumptionAddedResponse(decimal ConsumptionTotal);
public sealed record FinishSessionResponse(Guid MatchHistoryId, decimal GrandTotal);
public sealed record RoundResponse(Guid Id, int RoundNumber, int WhiteScore, int YellowScore, string? WinnerName);

public sealed record RoundDetailResponse(int RoundNumber, int WhiteScore, int YellowScore, string? WinnerName, DateTimeOffset EndedAt);
public sealed record RoundHistoryResponse(int WhiteRounds, int YellowRounds, int CurrentRoundNumber, IReadOnlyCollection<RoundDetailResponse> Rounds);

public sealed record ConsumptionAmountResponse(Guid Id, string ProductName, decimal UnitPrice, int Quantity, decimal Total, DateTimeOffset CreatedAt);

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