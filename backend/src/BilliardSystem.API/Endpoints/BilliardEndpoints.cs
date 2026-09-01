using System.Security.Cryptography;
using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using BilliardSystem.API.Hubs;

namespace BilliardSystem.API.Endpoints;

public static class BilliardEndpoints
{
    private static readonly HashSet<string> AllowedSettingKeys =
    [
        "HourlyRate",
        "ReplayBufferSeconds",
        "BusinessName"
    ];

    public static IEndpointRouteBuilder MapBilliardEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        api.MapGet("/health", () => Results.Ok(new { status = "ok", service = "BilliardSystem.API" }));

        // ── Auth ───────────────────────────────────────────────────────

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
                dbContext.Settings.Add(new AppSetting("AdminPassword", PasswordHasher.Hash(request.Password!)));
                var (session, rawToken) = CreateSession(dbContext);
                await dbContext.SaveChangesAsync(cancellationToken);
                return Results.Ok(new { token = rawToken });
            }

            if (!PasswordHasher.Verify(request.Password ?? string.Empty, adminPassword.Value))
            {
                return Results.Unauthorized();
            }

            if (PasswordHasher.IsLegacyHash(adminPassword.Value))
            {
                adminPassword.Update(PasswordHasher.Hash(request.Password!));
            }

            var (loginSession, loginToken) = CreateSession(dbContext);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { token = loginToken });
        }).RequireRateLimiting("Login");

        api.MapPost("/auth/change-password", async (
            ChangePasswordRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return Results.BadRequest(new { message = "La nueva clave debe tener al menos 8 caracteres." });
            }

            var adminPassword = await dbContext.Settings
                .FirstOrDefaultAsync(s => s.Key == "AdminPassword", cancellationToken);

            if (adminPassword is null || string.IsNullOrWhiteSpace(adminPassword.Value))
            {
                dbContext.Settings.Add(new AppSetting("AdminPassword", PasswordHasher.Hash(request.NewPassword!)));
            }
            else if (!PasswordHasher.Verify(request.CurrentPassword ?? string.Empty, adminPassword.Value))
            {
                return Results.Json(new { message = "La clave actual no coincide." }, statusCode: StatusCodes.Status401Unauthorized);
            }
            else
            {
                adminPassword.Update(PasswordHasher.Hash(request.NewPassword));
            }

            // Revoke all existing sessions
            await dbContext.Sessions.ExecuteDeleteAsync(cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization("AdminSession");

        api.MapPost("/auth/logout", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            await dbContext.Sessions.ExecuteDeleteAsync(cancellationToken);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization("AdminSession");

        // ── Tables (read-only, player + admin) ────────────────────────

        api.MapGet("/tables", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var tables = await dbContext.Tables
                .AsNoTracking()
                .OrderBy(table => table.Name)
                .Select(table => new TableResponse(
                    table.Id,
                    table.Name,
                    table.Code,
                    table.Status.ToString(),
                    table.HourlyRate,
                    table.IsActive,
                    table.ActiveMatchId))
                .ToListAsync(cancellationToken);

            return Results.Ok(tables);
        });

        api.MapGet("/tables/lookup/{identifier}", async (string identifier, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var table = await FindTableAsync(dbContext, identifier, cancellationToken);
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
                table.Code,
                table.Status.ToString(),
                table.HourlyRate,
                table.IsActive,
                table.ActiveMatchId,
                match is null ? null : ToMatchDetailResponse(match)));
        });

        api.MapGet("/tables/{identifier}", async (string identifier, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var table = await FindTableAsync(dbContext, identifier, cancellationToken);

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
                table.Code,
                table.Status.ToString(),
                table.HourlyRate,
                table.IsActive,
                table.ActiveMatchId,
                match is null ? null : ToMatchDetailResponse(match)));
        });

        // ── Tables (write, admin-only) ────────────────────────────────

        api.MapPost("/tables", async (CreateTableRequest request, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80)
            {
                return Results.BadRequest(new { message = "El nombre de la mesa es obligatorio (máx. 80 caracteres)." });
            }

            if (request.HourlyRate is < 0 or > 1_000_000)
            {
                return Results.BadRequest(new { message = "La tarifa debe estar entre 0 y 1.000.000." });
            }

            var rate = request.HourlyRate > 0 ? request.HourlyRate : await GetGlobalRateAsync(dbContext, cancellationToken);
            var code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim().ToUpperInvariant();
            if (code is null)
            {
                code = await NextTableCodeAsync(dbContext, cancellationToken);
            }

            if (await dbContext.Tables.AnyAsync(t => t.Code == code, cancellationToken))
            {
                return Results.BadRequest(new { message = $"El código '{code}' ya está en uso." });
            }

            var table = new BilliardTable(request.Name.Trim(), rate);
            table.SetCode(code);
            dbContext.Tables.Add(table);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapPut("/tables/{id}", async (Guid id, UpdateTableRequest request, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (request.Name.Length > 80)
                {
                    return Results.BadRequest(new { message = "El nombre no puede exceder 80 caracteres." });
                }
                table.Rename(request.Name.Trim());
            }

            if (!string.IsNullOrWhiteSpace(request.Code)
                && !string.Equals(table.Code, request.Code.Trim().ToUpperInvariant(), StringComparison.Ordinal)
                && await dbContext.Tables.AnyAsync(t => t.Code == request.Code.Trim().ToUpperInvariant(), cancellationToken))
            {
                return Results.BadRequest(new { message = $"El código '{request.Code}' ya está en uso." });
            }

            if (!string.IsNullOrWhiteSpace(request.Code))
            {
                table.SetCode(request.Code);
            }

            if (request.HourlyRate is > 0 and <= 1_000_000)
            {
                table.SetHourlyRate(request.HourlyRate);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapPut("/tables/rate/all", async (UpdateAllRatesRequest request, BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken cancellationToken) =>
        {
            if (request.HourlyRate is <= 0 or > 1_000_000)
            {
                return Results.BadRequest(new { message = "La tarifa debe ser mayor a cero y menor a 1.000.000." });
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

            await hub.Clients.Group("admins").SendAsync("TableStateUpdated", new { tableId = (Guid?)null, status = "RateChanged" }, cancellationToken);
            return Results.Ok(new { updated = tables.Count });
        }).RequireAuthorization("AdminSession");

        api.MapPost("/tables/{id}/attend", async (
            Guid id,
            BilliardDbContext dbContext,
            IHubContext<TableHub> hub,
            CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (table.Status is BilliardTableStatus.WaitingForWaiter or BilliardTableStatus.WaitingForCheck)
            {
                table.MarkAttended();
                await dbContext.SaveChangesAsync(cancellationToken);
                await hub.Clients.Group("admins").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);
                await hub.Clients.Group($"table:{id}").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);
            }

            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapPost("/tables/{id}/disable", async (
            Guid id,
            IHubContext<TableHub> hub,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (table.ActiveMatchId is not null)
            {
                return Results.BadRequest(new { message = "No se puede inhabilitar una mesa con partida activa." });
            }

            table.Disable();
            await dbContext.SaveChangesAsync(cancellationToken);
            await hub.Clients.Group("admins").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapPost("/tables/{id}/enable", async (
            Guid id,
            IHubContext<TableHub> hub,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            table.Enable();
            await dbContext.SaveChangesAsync(cancellationToken);
            await hub.Clients.Group("admins").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, cancellationToken);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapDelete("/tables/{id}", async (Guid id, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
            if (table is null)
            {
                return Results.NotFound();
            }

            if (table.ActiveMatchId is not null)
            {
                return Results.BadRequest(new { message = "No se puede borrar una mesa con partida activa. Ciérrala primero." });
            }

            var hasHistory = await dbContext.MatchHistories.AnyAsync(history => history.TableId == id, cancellationToken);
            if (hasHistory)
            {
                return Results.BadRequest(new { message = "Esta mesa tiene historial de partidas; inhabílitala en su lugar." });
            }

            dbContext.Tables.Remove(table);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.TableDeleted, null, id, null, null, $"Se eliminó la mesa '{table.Name}'.", cancellationToken);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization("AdminSession");

        // ── Products (read, player + admin) ───────────────────────────

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

        // ── Products (write, admin-only) ──────────────────────────────

        api.MapPost("/products", async (
            CreateProductRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
            {
                return Results.BadRequest(new { message = "El nombre del producto es obligatorio (máx. 120 caracteres)." });
            }

            if (request.Price is <= 0 or > 1_000_000)
            {
                return Results.BadRequest(new { message = "El precio debe estar entre 1 y 1.000.000." });
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
        }).RequireAuthorization("AdminSession");

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

            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
            {
                return Results.BadRequest(new { message = "El nombre es obligatorio (máx. 120 caracteres)." });
            }

            if (request.Price is <= 0 or > 1_000_000)
            {
                return Results.BadRequest(new { message = "El precio debe estar entre 1 y 1.000.000." });
            }

            product.Update(request.Name.Trim(), request.Price);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        }).RequireAuthorization("AdminSession");

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
        }).RequireAuthorization("AdminSession");

        // ── Settings (admin-only) ─────────────────────────────────────

        api.MapGet("/settings", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var settings = await dbContext.Settings
                .AsNoTracking()
                .Where(s => s.Key != "AdminPassword")
                .OrderBy(setting => setting.Key)
                .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

            return Results.Ok(settings);
        }).RequireAuthorization("AdminSession");

        api.MapPut("/settings", async (Dictionary<string, string> values, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            foreach (var pair in values)
            {
                if (!AllowedSettingKeys.Contains(pair.Key))
                {
                    continue;
                }

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
        }).RequireAuthorization("AdminSession");

        // ── Player operations (anonymous, kiosk) ──────────────────────

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
            if (request.Delta is < -5 or > 50)
            {
                return Results.BadRequest(new { message = "El puntaje debe estar entre -5 y 50." });
            }

            var color = request.PlayerColor?.Equals("yellow", StringComparison.OrdinalIgnoreCase) == true
                ? "yellow" : "white";

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

            var scoreLog = match.AddScore(color, request.Delta, request.UserId);
            dbContext.MatchScoreLogs.Add(scoreLog);
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.PlayerScored, request.UserId, table.Id, match.Id, request.TransactionId,
                $"Carambola {color} {request.Delta:+0;-0;0} -> {scoreLog.ResultingScore}", cancellationToken);

            await hub.Clients.Group($"table:{id}").SendAsync("PlayerScored", new
            {
                tableId = id,
                playerColor = color,
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
            if (string.IsNullOrWhiteSpace(request.WhitePlayerName) || string.IsNullOrWhiteSpace(request.YellowPlayerName))
            {
                return Results.BadRequest(new { message = "Los nombres de los jugadores son obligatorios." });
            }

            if (request.WhitePlayerName.Length > 80 || request.YellowPlayerName.Length > 80)
            {
                return Results.BadRequest(new { message = "Los nombres no pueden exceder 80 caracteres." });
            }

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

            match.RenamePlayer("white", request.WhitePlayerName.Trim());
            match.RenamePlayer("yellow", request.YellowPlayerName.Trim());
            await dbContext.SaveChangesAsync(cancellationToken);
            await WriteAuditAsync(dbContext, AuditActionType.PlayerNameChanged, request.UserId, table.Id, match.Id, request.TransactionId,
                "Nombres de jugadores actualizados", cancellationToken);

            await hub.Clients.Group($"table:{id}").SendAsync("PlayerNamesChanged", new
            {
                tableId = id,
                whitePlayerName = request.WhitePlayerName.Trim(),
                yellowPlayerName = request.YellowPlayerName.Trim()
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
            if (request.Quantity is < 1 or > 999)
            {
                return Results.BadRequest(new { message = "La cantidad debe estar entre 1 y 999." });
            }

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

            await hub.Clients.Group("admins").SendAsync("AdminNotification", new { type = "waiter", tableId = id, tableName = table.Name, timestamp = DateTimeOffset.UtcNow }, cancellationToken);
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

            await hub.Clients.Group("admins").SendAsync("AdminRequest", new { type = "check", tableId = id, tableName = table.Name, total = consumptionTotal, timestamp = DateTimeOffset.UtcNow }, cancellationToken);
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
                    round.EndedAt,
                    round.Duration))
                .ToArray();

            var whiteRounds = rounds.Count(r => r.WinnerName == match.WhitePlayerName);
            var yellowRounds = rounds.Count(r => r.WinnerName == match.YellowPlayerName);

            return Results.Ok(new RoundHistoryResponse(whiteRounds, yellowRounds, match.RoundNumber, rounds));
        });

        // ── History & Dashboard (admin-only) ──────────────────────────

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
        }).RequireAuthorization("AdminSession");

        api.MapGet("/matches/{id}", async (Guid id, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var match = await dbContext.MatchHistories
                .AsNoTracking()
                .Include(history => history.ScoreLogs)
                .Include(history => history.Consumptions)
                .FirstOrDefaultAsync(history => history.Id == id, cancellationToken);

            return match is null ? Results.NotFound() : Results.Ok(ToMatchDetailResponse(match));
        }).RequireAuthorization("AdminSession");

        api.MapGet("/dashboard/summary", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var today = DateTimeOffset.UtcNow.Date;
            var tables = await dbContext.Tables.AsNoTracking().Where(t => t.IsActive).ToListAsync(cancellationToken);
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
        }).RequireAuthorization("AdminSession");

        api.MapGet("/dashboard/top-products", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var start = DateTimeOffset.UtcNow.Date.AddDays(-7);
            var rows = await dbContext.MatchConsumptions
                .AsNoTracking()
                .Where(item => item.CreatedAt >= start)
                .GroupBy(item => item.ProductNameSnapshot)
                .Select(group => new { Name = group.Key, Quantity = group.Sum(item => item.Quantity), Total = group.Sum(item => item.UnitPriceSnapshot * item.Quantity) })
                .OrderByDescending(group => group.Quantity)
                .Take(10)
                .ToListAsync(cancellationToken);

            var items = rows.Select(row => new TopProductResponse(row.Name, row.Quantity, row.Total)).ToArray();
            return Results.Ok(items);
        }).RequireAuthorization("AdminSession");

        api.MapGet("/audit/logs", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var logs = await dbContext.AuditLogs
                .AsNoTracking()
                .OrderByDescending(log => log.CreatedAt)
                .Take(200)
                .Select(log => new AuditLogResponse(log.Id, log.ActionType.ToString(), log.Description, log.UserId, log.TableId, log.MatchId, log.TransactionId, log.CreatedAt))
                .ToListAsync(cancellationToken);

            return Results.Ok(logs);
        }).RequireAuthorization("AdminSession");

        return app;
    }

    private static (AdminSession session, string rawToken) CreateSession(BilliardDbContext dbContext)
    {
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var tokenHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken)));
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);
        var session = new AdminSession(tokenHash, expiresAt);
        dbContext.Sessions.Add(session);
        return (session, rawToken);
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

    private static async Task<BilliardTable?> FindTableAsync(BilliardDbContext dbContext, string identifier, CancellationToken cancellationToken)
    {
        if (Guid.TryParse(identifier, out var id))
        {
            return await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        }

        var code = identifier.Trim().ToUpperInvariant();
        return await dbContext.Tables.FirstOrDefaultAsync(t => t.Code == code, cancellationToken);
    }

    private static async Task<string> NextTableCodeAsync(BilliardDbContext dbContext, CancellationToken cancellationToken)
    {
        var codes = await dbContext.Tables.AsNoTracking().Select(t => t.Code).ToListAsync(cancellationToken);
        var used = new HashSet<string>(codes);
        var n = 1;
        while (used.Contains($"M{n}"))
        {
            n += 1;
        }

        return $"M{n}";
    }

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
}

#region Request / Response DTOs

public sealed record TableResponse(Guid Id, string Name, string Code, string Status, decimal HourlyRate, bool IsActive, Guid? ActiveMatchId);
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
    string Code,
    string Status,
    decimal HourlyRate,
    bool IsActive,
    Guid? ActiveMatchId,
    MatchDetailResponse? ActiveMatch);

public sealed record StartSessionRequest(string WhitePlayerName, string YellowPlayerName, GameMode GameMode, Guid? TransactionId, Guid? UserId);
public sealed record LoginRequest(string? Password);
public sealed record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
public sealed record CreateTableRequest(string Name, decimal HourlyRate, string? Code = null);
public sealed record UpdateTableRequest(string? Name, decimal HourlyRate, string? Code = null);
public sealed record UpdateAllRatesRequest(decimal HourlyRate);
public sealed record ScoreRequest(string PlayerColor, int Delta, Guid? TransactionId, Guid? UserId);
public sealed record RenamePlayersRequest(string WhitePlayerName, string YellowPlayerName, Guid? TransactionId, Guid? UserId);
public sealed record AddConsumptionRequest(Guid ProductId, int Quantity, Guid? TransactionId, Guid? UserId);
public sealed record CreateProductRequest(string Name, decimal Price);
public sealed record UpdateProductRequest(string Name, decimal Price);
public sealed record TableRequest(Guid? TransactionId, Guid? UserId);
public sealed record FinishSessionRequest(Guid? TransactionId, Guid? ClosedByUserId);
public sealed record StartSessionResponse(Guid TableId, Guid MatchId);
public sealed record ScoreResponse(int NewScore);
public sealed record ConsumptionAddedResponse(decimal ConsumptionTotal);
public sealed record FinishSessionResponse(Guid MatchHistoryId, decimal GrandTotal);
public sealed record RoundResponse(Guid Id, int RoundNumber, int WhiteScore, int YellowScore, string? WinnerName);

public sealed record RoundDetailResponse(int RoundNumber, int WhiteScore, int YellowScore, string? WinnerName, DateTimeOffset EndedAt, TimeSpan Duration);
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
