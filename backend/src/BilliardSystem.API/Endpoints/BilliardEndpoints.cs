using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using BilliardSystem.API.Auth;
using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
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

        api.MapPost("/auth/login", async (
            LoginRequest request,
            BilliardDbContext dbContext,
            IConfiguration config,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
            {
                return Results.BadRequest(new { message = "Nombre del local y clave son obligatorios." });
            }

            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => (u.UserName == request.UserName || u.DisplayName == request.UserName) && u.IsActive, cancellationToken);

            if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            if (user.Role == UserRole.SuperAdmin)
            {
                var (accessToken, refreshToken) = await CreateTokenPairAsync(dbContext, config, user, null);
                await dbContext.SaveChangesAsync(cancellationToken);
                return Results.Ok(new LoginResponse(accessToken, refreshToken, user.DisplayName, user.Role.ToString(), null, null, false));
            }

            if (user.TenantId is null)
            {
                return Results.BadRequest(new { message = "Usuario sin local asignado." });
            }

            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken);
            var (userAccessToken, userRefreshToken) = await CreateTokenPairAsync(dbContext, config, user, user.TenantId);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new LoginResponse(userAccessToken, userRefreshToken, user.DisplayName, user.Role.ToString(), tenant?.Name, tenant?.Slug, user.MustChangePassword));
        }).RequireRateLimiting("Login");

        api.MapPost("/auth/refresh", async (
            RefreshRequest request,
            BilliardDbContext dbContext,
            IConfiguration config,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return Results.BadRequest(new { message = "Refresh token requerido." });
            }

            var tokenHash = HashToken(request.RefreshToken);
            var session = await dbContext.Sessions
                .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

            if (session is null || !session.IsValid())
            {
                return Results.Unauthorized();
            }

            var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.UserId, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return Results.Unauthorized();
            }

            session.Revoke();
            var (accessToken, refreshToken) = await CreateTokenPairAsync(dbContext, config, user, session.TenantId);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { accessToken, refreshToken });
        }).RequireRateLimiting("Login");

        api.MapPost("/auth/logout", async (
            RefreshRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                var tokenHash = HashToken(request.RefreshToken);
                var session = await dbContext.Sessions.FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);
                if (session is not null)
                {
                    session.Revoke();
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            return Results.Ok(new { ok = true });
        });

        api.MapPost("/auth/force-change-password", async (
            ChangePasswordRequest request,
            BilliardDbContext dbContext,
            IConfiguration config,
            ClaimsPrincipal userPrincipal,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return Results.BadRequest(new { message = "La nueva clave debe tener al menos 8 caracteres." });
            }

            var userId = userPrincipal.GetUserId();
            if (userId == Guid.Empty) return Results.Unauthorized();

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
            if (user is null)
            {
                return Results.Unauthorized();
            }

            user.SetPassword(PasswordHasher.Hash(request.NewPassword));
            user.ClearMustChangePassword();
            await dbContext.Sessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);

            var (accessToken, refreshToken) = await CreateTokenPairAsync(dbContext, config, user, user.TenantId);
            await dbContext.SaveChangesAsync(cancellationToken);
            var tenant = user.TenantId.HasValue
                ? await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId, cancellationToken)
                : null;
            return Results.Ok(new LoginResponse(accessToken, refreshToken, user.DisplayName, user.Role.ToString(), tenant?.Name, tenant?.Slug, false));
        }).RequireAuthorization("AdminSession");

        api.MapPost("/auth/change-password", async (
            ChangePasswordRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 8)
            {
                return Results.BadRequest(new { message = "La nueva clave debe tener al menos 8 caracteres." });
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);
            if (user is null || !PasswordHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
            {
                return Results.Unauthorized();
            }

            user.SetPassword(PasswordHasher.Hash(request.NewPassword));
            await dbContext.Sessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization("AdminSession");

        api.MapPost("/auth/forgot", async (
            ForgotPasswordRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName))
            {
                return Results.BadRequest(new { message = "Ingresa el nombre de tu local." });
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(
                u => (u.UserName == request.UserName || u.DisplayName == request.UserName) && u.IsActive, cancellationToken);

            if (user is null || user.TenantId is null)
            {
                return Results.Ok(new { message = "Si el local existe, solicita el código a tu administrador." });
            }

            var pendingExists = await dbContext.RecoveryRequests.AnyAsync(
                r => r.UserId == user.Id && !r.IsResolved && !r.IsExpired(), cancellationToken);
            if (pendingExists)
            {
                return Results.Ok(new { message = "Ya hay un código activo. Solicítalo a tu administrador." });
            }

            var code = GenerateRecoveryCode();
            var codeHash = HashToken(code);
            var recovery = new RecoveryRequest(user.TenantId.Value, user.Id, codeHash, DateTimeOffset.UtcNow.AddMinutes(30));
            dbContext.RecoveryRequests.Add(recovery);
            await dbContext.SaveChangesAsync(cancellationToken);

            return Results.Ok(new { message = "Solicitud creada. Solicita el código a tu administrador." });
        }).RequireRateLimiting("Login");

        api.MapPost("/auth/reset", async (
            ResetPasswordRequest request,
            BilliardDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.NewPassword))
            {
                return Results.BadRequest(new { message = "Todos los campos son obligatorios." });
            }
            if (request.NewPassword.Length < 8)
            {
                return Results.BadRequest(new { message = "La nueva clave debe tener al menos 8 caracteres." });
            }

            var user = await dbContext.Users.FirstOrDefaultAsync(
                u => (u.UserName == request.UserName || u.DisplayName == request.UserName) && u.IsActive, cancellationToken);
            if (user is null)
            {
                return Results.BadRequest(new { message = "Código inválido o expirado." });
            }

            var codeHash = HashToken(request.Code);
            var recovery = await dbContext.RecoveryRequests.FirstOrDefaultAsync(
                r => r.UserId == user.Id && r.CodeHash == codeHash && !r.IsResolved, cancellationToken);

            if (recovery is null || recovery.IsExpired())
            {
                return Results.BadRequest(new { message = "Código inválido o expirado." });
            }

            user.SetPassword(PasswordHasher.Hash(request.NewPassword));
            recovery.Resolve();
            await dbContext.Sessions.Where(s => s.UserId == user.Id).ExecuteDeleteAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new { ok = true });
        }).RequireRateLimiting("Login");

        // ── Super Admin ────────────────────────────────────────────────

        var super = api.MapGroup("/super").RequireAuthorization("SuperAdmin");

        super.MapGet("/locals", async (BilliardDbContext dbContext, CancellationToken ct) =>
        {
            var tenants = await dbContext.Tenants
                .AsNoTracking()
                .Select(t => new LocalResponse(t.Id, t.Name, t.Slug, t.IsActive,
                    dbContext.Tables.Count(tb => tb.TenantId == t.Id),
                    dbContext.Users.Count(u => u.TenantId == t.Id)))
                .ToListAsync(ct);
            return Results.Ok(tenants);
        });

        super.MapPost("/locals", async (CreateLocalRequest request, BilliardDbContext dbContext, IConfiguration config, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
                return Results.BadRequest(new { message = "Nombre del local obligatorio (máx. 120 caracteres)." });

            var tenant = new Tenant(request.Name);
            if (await dbContext.Tenants.AnyAsync(t => t.Slug == tenant.Slug, ct))
                return Results.BadRequest(new { message = "Ya existe un local con un nombre similar." });

            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync(ct);

            var defaultPassword = string.IsNullOrWhiteSpace(request.InitialPassword) ? "admin123" : request.InitialPassword;
            var user = new User(request.Name, tenant.Slug, PasswordHasher.Hash(defaultPassword), UserRole.Administrator, tenant.Id);
            user.ClearMustChangePassword();
            dbContext.Users.Add(user);

            var table = new BilliardTable("Mesa 1", 12000m, tenant.Id);
            table.SetCode("M1");
            dbContext.Tables.Add(table);
            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(new CreateLocalResponse(tenant.Id, tenant.Name, tenant.Slug, defaultPassword));
        });

        super.MapGet("/recoveries", async (BilliardDbContext dbContext, CancellationToken ct) =>
        {
            var requests = await dbContext.RecoveryRequests
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Tenant)
                .Where(r => !r.IsResolved && r.ExpiresAt > DateTimeOffset.UtcNow)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new RecoveryCodeResponse(r.Id, r.Tenant!.Name, r.User!.UserName, r.CreatedAt, r.ExpiresAt))
                .ToListAsync(ct);
            return Results.Ok(requests);
        });

        super.MapPost("/recoveries/{id}/reveal", async (
            Guid id, BilliardDbContext dbContext, CancellationToken ct) =>
        {
            var request = await dbContext.RecoveryRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id && !r.IsResolved, ct);
            if (request is null) return Results.NotFound();

            var code = GenerateRecoveryCode();
            var codeHash = HashToken(code);
            request.Resolve();
            await dbContext.SaveChangesAsync(ct);

            return Results.Ok(new { code, userName = request.User?.UserName });
        });

        // ── Tables (read-only, player + admin) ────────────────────────

        api.MapGet("/tables", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var tables = await dbContext.Tables
                .AsNoTracking()
                .OrderBy(table => table.Name)
                .Select(table => new TableResponse(
                    table.Id, table.Name, table.Code, table.Status.ToString(),
                    table.HourlyRate, table.IsActive, table.ActiveMatchId))
                .ToListAsync(cancellationToken);
            return Results.Ok(tables);
        });

        api.MapGet("/t/{slug}/tables", async (string slug, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
            if (tenant is null) return Results.NotFound();

            var tables = await dbContext.Tables
                .AsNoTracking()
                .Where(t => t.TenantId == tenant.Id)
                .OrderBy(table => table.Name)
                .Select(table => new TableResponse(
                    table.Id, table.Name, table.Code, table.Status.ToString(),
                    table.HourlyRate, table.IsActive, table.ActiveMatchId))
                .ToListAsync(cancellationToken);
            return Results.Ok(tables);
        });

        api.MapGet("/tables/{identifier}", async (string identifier, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var table = await FindTableAsync(dbContext, identifier, cancellationToken);
            if (table is null) return Results.NotFound();
            return Results.Ok(await ToTableDetailAsync(dbContext, table, cancellationToken));
        });

        api.MapGet("/t/{slug}/tables/{identifier}", async (string slug, string identifier, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
            if (tenant is null) return Results.NotFound();

            BilliardTable? table;
            if (Guid.TryParse(identifier, out var id))
                table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.Id, cancellationToken);
            else
            {
                var code = identifier.Trim().ToUpperInvariant();
                table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Code == code && t.TenantId == tenant.Id, cancellationToken);
            }

            if (table is null) return Results.NotFound();
            return Results.Ok(await ToTableDetailAsync(dbContext, table, cancellationToken));
        });

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

        api.MapGet("/t/{slug}/products", async (string slug, BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken);
            if (tenant is null) return Results.NotFound();

            var products = await dbContext.Products
                .AsNoTracking()
                .Where(p => p.TenantId == tenant.Id && p.IsActive)
                .OrderBy(p => p.Name)
                .Select(p => new ProductResponse(p.Id, p.Name, p.Price))
                .ToListAsync(cancellationToken);
            return Results.Ok(products);
        });

        // ── Tables (write, admin-only) ────────────────────────────────

        api.MapPost("/tables", async (
            CreateTableRequest request,
            BilliardDbContext dbContext,
            ClaimsPrincipal user,
            CancellationToken cancellationToken) =>
        {
            var tenantId = user.GetTenantId();
            if (tenantId is null) return Results.Forbid();

            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 80)
                return Results.BadRequest(new { message = "El nombre de la mesa es obligatorio (máx. 80 caracteres)." });
            if (request.HourlyRate is < 0 or > 1_000_000)
                return Results.BadRequest(new { message = "La tarifa debe estar entre 0 y 1.000.000." });

            var rate = request.HourlyRate > 0 ? request.HourlyRate : await GetGlobalRateAsync(dbContext, tenantId.Value, cancellationToken);
            var code = string.IsNullOrWhiteSpace(request.Code) ? await NextTableCodeAsync(dbContext, tenantId.Value, cancellationToken) : request.Code.Trim().ToUpperInvariant();

            if (await dbContext.Tables.AnyAsync(t => t.TenantId == tenantId && t.Code == code, cancellationToken))
                return Results.BadRequest(new { message = $"El código '{code}' ya está en uso." });

            var table = new BilliardTable(request.Name.Trim(), rate, tenantId.Value);
            table.SetCode(code);
            dbContext.Tables.Add(table);
            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapPut("/tables/{id}", async (
            Guid id, UpdateTableRequest request, BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);
            if (table is null) return Results.NotFound();

            if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Length <= 80) table.Rename(request.Name.Trim());
            if (!string.IsNullOrWhiteSpace(request.Code) && !string.Equals(table.Code, request.Code.Trim().ToUpperInvariant(), StringComparison.Ordinal))
            {
                if (await dbContext.Tables.AnyAsync(t => t.TenantId == tenantId && t.Code == request.Code.Trim().ToUpperInvariant(), ct))
                    return Results.BadRequest(new { message = $"El código '{request.Code}' ya está en uso." });
                table.SetCode(request.Code);
            }
            if (request.HourlyRate is > 0 and <= 1_000_000) table.SetHourlyRate(request.HourlyRate);

            await dbContext.SaveChangesAsync(ct);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapPut("/tables/rate/all", async (
            UpdateAllRatesRequest request, BilliardDbContext dbContext, ClaimsPrincipal user,
            IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            if (tenantId is null) return Results.Forbid();
            if (request.HourlyRate is <= 0 or > 1_000_000)
                return Results.BadRequest(new { message = "La tarifa debe ser mayor a cero y menor a 1.000.000." });

            var tables = await dbContext.Tables.Where(t => t.TenantId == tenantId).ToListAsync(ct);
            foreach (var table in tables) table.SetHourlyRate(request.HourlyRate);

            var rateSetting = await dbContext.Settings.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == "HourlyRate", ct);
            if (rateSetting is null) dbContext.Settings.Add(new AppSetting("HourlyRate", request.HourlyRate.ToString(), tenantId));
            else rateSetting.Update(request.HourlyRate.ToString());

            await dbContext.SaveChangesAsync(ct);
            await hub.Clients.Group($"admins:{tenantId}").SendAsync("TableStateUpdated", new { tableId = (Guid?)null, status = "RateChanged" }, ct);
            return Results.Ok(new { updated = tables.Count });
        }).RequireAuthorization("AdminSession");

        api.MapPost("/tables/{id}/attend", async (Guid id, BilliardDbContext dbContext, ClaimsPrincipal user, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);
            if (table is null) return Results.NotFound();

            if (table.Status is BilliardTableStatus.WaitingForWaiter or BilliardTableStatus.WaitingForCheck)
            {
                table.MarkAttended();
                await dbContext.SaveChangesAsync(ct);
                await hub.Clients.Group($"admins:{tenantId}").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, ct);
                await hub.Clients.Group($"table:{id}").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, ct);
            }
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapPost("/tables/{id}/disable", async (Guid id, BilliardDbContext dbContext, ClaimsPrincipal user, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);
            if (table is null) return Results.NotFound();
            if (table.ActiveMatchId is not null) return Results.BadRequest(new { message = "No se puede inhabilitar una mesa con partida activa." });
            table.Disable();
            await dbContext.SaveChangesAsync(ct);
            await hub.Clients.Group($"admins:{tenantId}").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, ct);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapPost("/tables/{id}/enable", async (Guid id, BilliardDbContext dbContext, ClaimsPrincipal user, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);
            if (table is null) return Results.NotFound();
            table.Enable();
            await dbContext.SaveChangesAsync(ct);
            await hub.Clients.Group($"admins:{tenantId}").SendAsync("TableStateUpdated", new { tableId = id, status = table.Status.ToString() }, ct);
            return Results.Ok(new TableResponse(table.Id, table.Name, table.Code, table.Status.ToString(), table.HourlyRate, table.IsActive, table.ActiveMatchId));
        }).RequireAuthorization("AdminSession");

        api.MapDelete("/tables/{id}", async (Guid id, BilliardDbContext dbContext, ClaimsPrincipal user, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId, ct);
            if (table is null) return Results.NotFound();
            if (table.ActiveMatchId is not null) return Results.BadRequest(new { message = "No se puede borrar una mesa con partida activa." });
            var hasHistory = await dbContext.MatchHistories.AnyAsync(h => h.TableId == id, ct);
            if (hasHistory) return Results.BadRequest(new { message = "Esta mesa tiene historial de partidas; inhabílitala en su lugar." });
            dbContext.Tables.Remove(table);
            await dbContext.SaveChangesAsync(ct);
            await WriteAuditAsync(dbContext, AuditActionType.TableDeleted, null, id, null, null, $"Se eliminó la mesa '{table.Name}'.", tenantId, ct);
            return Results.Ok(new { ok = true });
        }).RequireAuthorization("AdminSession");

        // ── Products (write, admin-only) ──────────────────────────────

        api.MapPost("/products", async (CreateProductRequest request, BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            if (tenantId is null) return Results.Forbid();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
                return Results.BadRequest(new { message = "El nombre del producto es obligatorio (máx. 120 caracteres)." });
            if (request.Price is <= 0 or > 1_000_000)
                return Results.BadRequest(new { message = "El precio debe estar entre 1 y 1.000.000." });

            var category = await dbContext.Categories
                .Where(c => c.TenantId == tenantId && c.IsActive)
                .OrderBy(c => c.SortOrder)
                .FirstOrDefaultAsync(ct);
            if (category is null)
            {
                category = new ProductCategory("General", tenantId.Value);
                dbContext.Categories.Add(category);
                await dbContext.SaveChangesAsync(ct);
            }

            var product = new Product(category.Id, request.Name.Trim(), request.Price, tenantId.Value);
            dbContext.Products.Add(product);
            await dbContext.SaveChangesAsync(ct);
            return Results.Ok(new ProductResponse(product.Id, product.Name, product.Price));
        }).RequireAuthorization("AdminSession");

        api.MapPut("/products/{id}", async (Guid id, UpdateProductRequest request, BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);
            if (product is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 120)
                return Results.BadRequest(new { message = "El nombre es obligatorio (máx. 120 caracteres)." });
            if (request.Price is <= 0 or > 1_000_000)
                return Results.BadRequest(new { message = "El precio debe estar entre 1 y 1.000.000." });
            product.Update(request.Name.Trim(), request.Price);
            await dbContext.SaveChangesAsync(ct);
            return Results.Ok();
        }).RequireAuthorization("AdminSession");

        api.MapDelete("/products/{id}", async (Guid id, BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == tenantId, ct);
            if (product is null) return Results.NotFound();
            product.Deactivate();
            await dbContext.SaveChangesAsync(ct);
            return Results.Ok();
        }).RequireAuthorization("AdminSession");

        // ── Settings (admin-only) ─────────────────────────────────────

        api.MapGet("/settings", async (BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var settings = await dbContext.Settings
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.Key != "AdminPassword")
                .OrderBy(s => s.Key)
                .ToDictionaryAsync(s => s.Key, s => s.Value, ct);
            return Results.Ok(settings);
        }).RequireAuthorization("AdminSession");

        api.MapPut("/settings", async (Dictionary<string, string> values, BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            if (tenantId is null) return Results.Forbid();
            foreach (var pair in values)
            {
                if (!AllowedSettingKeys.Contains(pair.Key)) continue;
                var setting = await dbContext.Settings.FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == pair.Key, ct);
                if (setting is null) dbContext.Settings.Add(new AppSetting(pair.Key, pair.Value, tenantId));
                else setting.Update(pair.Value);
            }
            await dbContext.SaveChangesAsync(ct);
            return Results.Ok();
        }).RequireAuthorization("AdminSession");

        // ── Player operations (anonymous, kiosk) ──────────────────────

        api.MapPost("/t/{slug}/tables/{id}/start", async (
            string slug, Guid id, StartSessionRequest request,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is null) return Results.NotFound();

            if (await IsIdempotentAsync(dbContext, request.TransactionId, ct)) return Results.Ok();

            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.Id, ct);
            if (table is null) return Results.NotFound();

            if (request.GameMode == GameMode.FreeMode && (!string.IsNullOrWhiteSpace(request.ConsumptionProduct) || request.ConsumptionQuantity > 0))
                return Results.BadRequest(new { message = "El modo libre no permite agregar consumo." });

            if (table.ActiveMatchId is { } staleMatchId && request.GameMode == GameMode.FreeMode)
            {
                var staleMatch = await dbContext.MatchHistories.FirstOrDefaultAsync(h => h.Id == staleMatchId, ct);
                if (staleMatch is not null && staleMatch.GameMode == GameMode.FreeMode)
                {
                    var endedAt = DateTimeOffset.UtcNow;
                    staleMatch.Close(endedAt, 0, 0, null);
                    table.EndSession(staleMatch.Id, null);
                    await dbContext.SaveChangesAsync(ct);
                    await WriteAuditAsync(dbContext, AuditActionType.SessionEnded, null, table.Id, staleMatch.Id, null,
                        $"Sesión libre stale cerrada automáticamente en {table.Name}", tenant.Id, ct);
                }
            }

            var match = new MatchHistory(
                table.Id, request.WhitePlayerName, request.YellowPlayerName,
                table.HourlyRate, null, request.GameMode, tenant.Id);

            table.StartSession(match.Id, request.WhitePlayerName, request.YellowPlayerName, null);
            dbContext.MatchHistories.Add(match);
            await dbContext.SaveChangesAsync(ct);
            await WriteAuditAsync(dbContext, AuditActionType.SessionStarted, null, table.Id, match.Id, request.TransactionId,
                $"Inicio en {table.Name} (modo {request.GameMode})", tenant.Id, ct);

            await hub.Clients.Group($"table:{id}").SendAsync("SessionStarted", new { tableId = table.Id, matchId = match.Id }, ct);
            await hub.Clients.Group($"admins:{tenant.Id}").SendAsync("TableStateUpdated", new { tableId = table.Id, status = "Occupied" }, ct);
            return Results.Ok(new StartSessionResponse(table.Id, match.Id));
        });

        api.MapPost("/t/{slug}/tables/{id}/score", async (
            string slug, Guid id, ScoreRequest request,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            if (request.Delta is < -5 or > 50) return Results.BadRequest(new { message = "El puntaje debe estar entre -5 y 50." });
            var color = request.PlayerColor?.Equals("yellow", StringComparison.OrdinalIgnoreCase) == true ? "yellow" : "white";

            if (await IsIdempotentAsync(dbContext, request.TransactionId, ct)) return Results.Ok();

            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (table?.ActiveMatchId is not { } matchId) return Results.BadRequest("No hay partida activa en esta mesa.");
            var match = await dbContext.MatchHistories.FirstOrDefaultAsync(h => h.Id == matchId, ct);
            if (match is null) return Results.NotFound();

            var scoreLog = match.AddScore(color, request.Delta, request.UserId);
            dbContext.MatchScoreLogs.Add(scoreLog);
            await dbContext.SaveChangesAsync(ct);
            await WriteAuditAsync(dbContext, AuditActionType.PlayerScored, request.UserId, table.Id, match.Id, request.TransactionId,
                $"Carambola {color} {request.Delta:+0;-0;0} -> {scoreLog.ResultingScore}", table.TenantId, ct);

            await hub.Clients.Group($"table:{id}").SendAsync("PlayerScored", new
            {
                tableId = id, playerColor = color, delta = request.Delta,
                newScore = scoreLog.ResultingScore, totalCarambolas = match.TotalCarambolas
            }, ct);
            return Results.Ok(new ScoreResponse(scoreLog.ResultingScore));
        });

        api.MapPost("/t/{slug}/tables/{id}/players", async (
            string slug, Guid id, RenamePlayersRequest request,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.WhitePlayerName) || string.IsNullOrWhiteSpace(request.YellowPlayerName))
                return Results.BadRequest(new { message = "Los nombres de los jugadores son obligatorios." });
            if (request.WhitePlayerName.Length > 80 || request.YellowPlayerName.Length > 80)
                return Results.BadRequest(new { message = "Los nombres no pueden exceder 80 caracteres." });
            if (await IsIdempotentAsync(dbContext, request.TransactionId, ct)) return Results.Ok();

            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (table?.ActiveMatchId is not { } matchId) return Results.NotFound();
            var match = await dbContext.MatchHistories.FirstOrDefaultAsync(h => h.Id == matchId, ct);
            if (match is null) return Results.NotFound();

            match.RenamePlayer("white", request.WhitePlayerName.Trim());
            match.RenamePlayer("yellow", request.YellowPlayerName.Trim());
            await dbContext.SaveChangesAsync(ct);

            await hub.Clients.Group($"table:{id}").SendAsync("PlayerNamesChanged", new
            {
                tableId = id, whitePlayerName = request.WhitePlayerName.Trim(), yellowPlayerName = request.YellowPlayerName.Trim()
            }, ct);
            return Results.Ok();
        });

        api.MapPost("/t/{slug}/tables/{id}/consumption", async (
            string slug, Guid id, AddConsumptionRequest request,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            if (request.Quantity is < 1 or > 999) return Results.BadRequest(new { message = "La cantidad debe estar entre 1 y 999." });
            if (await IsIdempotentAsync(dbContext, request.TransactionId, ct)) return Results.Ok();

            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is null) return Results.NotFound();

            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.Id, ct);
            var product = await dbContext.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.TenantId == tenant.Id, ct);
            if (table?.ActiveMatchId is not { } matchId || product is null)
                return Results.BadRequest("Partida o producto inexistente.");

            var match = await dbContext.MatchHistories.Include(h => h.Consumptions).FirstOrDefaultAsync(h => h.Id == matchId, ct);
            if (match is null) return Results.NotFound();

            if (match.GameMode == GameMode.FreeMode)
                return Results.BadRequest(new { message = "El modo libre no permite agregar consumo." });

            var consumption = match.AddConsumption(product.Id, product.Name, product.Price, request.Quantity);
            dbContext.MatchConsumptions.Add(consumption);
            await dbContext.SaveChangesAsync(ct);

            await hub.Clients.Group($"table:{id}").SendAsync("ConsumptionAdded", new
            {
                tableId = id,
                item = new ConsumptionAmountResponse(consumption.Id, product.Name, product.Price, request.Quantity, product.Price * request.Quantity, consumption.CreatedAt),
                consumptionTotal = match.ConsumptionTotal
            }, ct);
            return Results.Ok(new ConsumptionAddedResponse(match.ConsumptionTotal));
        });

        api.MapPut("/t/{slug}/tables/{id}/consumption/{consumptionId}", async (
            string slug, Guid id, Guid consumptionId, UpdateConsumptionRequest request,
            BilliardDbContext dbContext, ClaimsPrincipal user, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            if (request.Quantity is < 1 or > 999) return Results.BadRequest(new { message = "La cantidad debe estar entre 1 y 999." });
            if (await IsIdempotentAsync(dbContext, request.TransactionId, ct)) return Results.Ok();

            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is null) return Results.NotFound();

            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.Id, ct);
            if (table?.ActiveMatchId is not { } matchId) return Results.BadRequest("No hay partida activa.");

            var match = await dbContext.MatchHistories.Include(h => h.Consumptions).FirstOrDefaultAsync(h => h.Id == matchId, ct);
            if (match is null) return Results.NotFound();

            match.UpdateConsumption(consumptionId, request.Quantity);
            await dbContext.SaveChangesAsync(ct);
            await WriteAuditAsync(dbContext, AuditActionType.ConsumptionUpdated, null, table.Id, match.Id, request.TransactionId,
                $"Consumo actualizado cantidad={request.Quantity}", table.TenantId, ct);

            await hub.Clients.Group($"table:{id}").SendAsync("ConsumptionAdded", new
            {
                tableId = id,
                item = (object?)null,
                consumptionTotal = match.ConsumptionTotal
            }, ct);
            return Results.Ok(new ConsumptionAddedResponse(match.ConsumptionTotal));
        });

        api.MapDelete("/t/{slug}/tables/{id}/consumption/{consumptionId}", async (
            string slug, Guid id, Guid consumptionId, Guid? transactionId,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            if (await IsIdempotentAsync(dbContext, transactionId, ct)) return Results.Ok();

            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is null) return Results.NotFound();

            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.Id, ct);
            if (table?.ActiveMatchId is not { } matchId) return Results.BadRequest("No hay partida activa.");

            var match = await dbContext.MatchHistories.Include(h => h.Consumptions).FirstOrDefaultAsync(h => h.Id == matchId, ct);
            if (match is null) return Results.NotFound();

            match.RemoveConsumption(consumptionId);
            await dbContext.SaveChangesAsync(ct);
            await WriteAuditAsync(dbContext, AuditActionType.ConsumptionRemoved, null, table.Id, match.Id, transactionId,
                $"Consumo {consumptionId} eliminado", table.TenantId, ct);

            await hub.Clients.Group($"table:{id}").SendAsync("ConsumptionAdded", new
            {
                tableId = id,
                item = (object?)null,
                consumptionTotal = match.ConsumptionTotal
            }, ct);
            return Results.Ok(new ConsumptionAddedResponse(match.ConsumptionTotal));
        });

        api.MapPost("/t/{slug}/tables/{id}/call-waiter", async (
            string slug, Guid id, TableRequest? request,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is null) return Results.NotFound();
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.Id, ct);
            if (table is null) return Results.NotFound();

            if (table.ActiveMatchId is { } matchId)
            {
                table.MarkWaiterRequested(matchId);
                await dbContext.SaveChangesAsync(ct);
                await WriteAuditAsync(dbContext, AuditActionType.WaiterRequested, null, table.Id, matchId, null, "Llamada de mesero", tenant.Id, ct);
            }
            await hub.Clients.Group($"admins:{tenant.Id}").SendAsync("AdminNotification", new { type = "waiter", tableId = id, tableName = table.Name, timestamp = DateTimeOffset.UtcNow }, ct);
            return Results.Ok();
        });

        api.MapPost("/t/{slug}/tables/{id}/request-check", async (
            string slug, Guid id, TableRequest? request,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is null) return Results.NotFound();
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.Id, ct);
            if (table is null) return Results.NotFound();

            var consumptionTotal = 0m;
            if (table.ActiveMatchId is { } activeMatch)
            {
                consumptionTotal = await dbContext.MatchHistories.Where(h => h.Id == activeMatch).Select(h => h.ConsumptionTotal).FirstOrDefaultAsync(ct);
                table.MarkCheckRequested(activeMatch);
                await dbContext.SaveChangesAsync(ct);
                await WriteAuditAsync(dbContext, AuditActionType.CheckRequested, null, table.Id, activeMatch, null, "Solicitud de cuenta", tenant.Id, ct);
            }
            await hub.Clients.Group($"admins:{tenant.Id}").SendAsync("AdminRequest", new { type = "check", tableId = id, tableName = table.Name, total = consumptionTotal, timestamp = DateTimeOffset.UtcNow }, ct);
            return Results.Ok();
        });

        api.MapPost("/t/{slug}/tables/{id}/finish", async (
            string slug, Guid id, FinishSessionRequest request,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            if (await IsIdempotentAsync(dbContext, request.TransactionId, ct)) return Results.Ok();

            var tenant = await dbContext.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug, ct);
            if (tenant is null) return Results.NotFound();
            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenant.Id, ct);
            if (table is null) return Results.NotFound();
            if (table.ActiveMatchId is not { } matchId) return Results.BadRequest("No hay partida activa.");

            var match = await dbContext.MatchHistories.Include(h => h.Consumptions).FirstOrDefaultAsync(h => h.Id == matchId, ct);
            if (match is null) return Results.NotFound();

            var endedAt = DateTimeOffset.UtcNow;
            var elapsedSeconds = Math.Max(0, (int)(endedAt - match.StartedAt).TotalSeconds);
            var tableTotal = Math.Round((elapsedSeconds / 3600m) * match.HourlyRateSnapshot, 2);
            match.Close(endedAt, tableTotal, match.ConsumptionTotal, null);
            table.EndSession(match.Id, null);
            await dbContext.SaveChangesAsync(ct);

            await hub.Clients.Group($"table:{id}").SendAsync("SessionEnded", new
            {
                tableId = id, matchHistoryId = match.Id,
                tableTotal = match.TableTotal, consumptionTotal = match.ConsumptionTotal, grandTotal = match.GrandTotal,
                winnerName = match.WhiteScore >= match.YellowScore ? match.WhitePlayerName : match.YellowPlayerName
            }, ct);
            await hub.Clients.Group($"admins:{tenant.Id}").SendAsync("TableStateUpdated", new { tableId = id, status = "Available" }, ct);
            return Results.Ok(new FinishSessionResponse(match.Id, match.GrandTotal));
        });

        api.MapPost("/t/{slug}/tables/{id}/finish-round", async (
            string slug, Guid id, TableRequest request,
            BilliardDbContext dbContext, IHubContext<TableHub> hub, CancellationToken ct) =>
        {
            if (await IsIdempotentAsync(dbContext, request.TransactionId, ct)) return Results.Ok();

            var table = await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, ct);
            if (table?.ActiveMatchId is not { } matchId) return Results.BadRequest("No hay partida activa.");
            var match = await dbContext.MatchHistories.FirstOrDefaultAsync(h => h.Id == matchId, ct);
            if (match is null) return Results.NotFound();

            var round = match.CloseRound();
            dbContext.MatchRounds.Add(round);
            await dbContext.SaveChangesAsync(ct);
            await WriteAuditAsync(dbContext, AuditActionType.RoundCompleted, request.UserId, table.Id, match.Id, request.TransactionId,
                round.WinnerName is null ? $"Ronda {round.RoundNumber} en {table.Name}: empate"
                    : $"Ronda {round.RoundNumber}: gana {round.WinnerName}", table.TenantId, ct);

            await hub.Clients.Group($"table:{id}").SendAsync("PlayerScored", new
            {
                tableId = id, playerColor = "white", delta = 0, newScore = 0, totalCarambolas = 0
            }, ct);
            return Results.Ok(new RoundResponse(round.Id, round.RoundNumber, round.WhiteScore, round.YellowScore, round.WinnerName));
        });

        api.MapGet("/t/{slug}/tables/{id}/rounds", async (string slug, Guid id, BilliardDbContext dbContext, CancellationToken ct) =>
        {
            var table = await dbContext.Tables.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);
            if (table is null) return Results.NotFound();

            Guid? matchId = table.ActiveMatchId;
            if (matchId is null)
            {
                var lastMatch = await dbContext.MatchHistories.AsNoTracking()
                    .Where(h => h.TableId == id)
                    .OrderByDescending(h => h.StartedAt)
                    .FirstOrDefaultAsync(ct);
                matchId = lastMatch?.Id;
            }

            if (matchId is null) return Results.Ok(new RoundHistoryResponse(0, 0, 0, []));

            var match = await dbContext.MatchHistories.AsNoTracking().Include(h => h.Rounds).FirstOrDefaultAsync(h => h.Id == matchId, ct);
            if (match is null) return Results.NotFound();

            var rounds = match.Rounds.OrderBy(r => r.RoundNumber)
                .Select(r => new RoundDetailResponse(r.RoundNumber, r.WhiteScore, r.YellowScore, r.WinnerName, r.EndedAt, r.Duration)).ToArray();
            var whiteRounds = rounds.Count(r => r.WinnerName == match.WhitePlayerName);
            var yellowRounds = rounds.Count(r => r.WinnerName == match.YellowPlayerName);
            return Results.Ok(new RoundHistoryResponse(whiteRounds, yellowRounds, match.RoundNumber, rounds));
        });

        // ── History & Dashboard (admin-only) ──────────────────────────

        api.MapGet("/matches", async (BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var matches = await dbContext.MatchHistories.AsNoTracking()
                .Where(h => h.TenantId == tenantId)
                .OrderByDescending(h => h.StartedAt)
                .Select(h => new MatchListItemResponse(h.Id, h.TableId, h.WhitePlayerName, h.YellowPlayerName,
                    h.WhiteScore, h.YellowScore, h.TotalCarambolas, h.GameMode.ToString(),
                    h.StartedAt, h.EndedAt, h.GrandTotal))
                .ToListAsync(ct);
            return Results.Ok(matches);
        }).RequireAuthorization("AdminSession");

        api.MapGet("/matches/{id}", async (Guid id, BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var match = await dbContext.MatchHistories.AsNoTracking()
                .Include(h => h.ScoreLogs).Include(h => h.Consumptions)
                .FirstOrDefaultAsync(h => h.Id == id && h.TenantId == tenantId, ct);
            return match is null ? Results.NotFound() : Results.Ok(ToMatchDetailResponse(match));
        }).RequireAuthorization("AdminSession");

        api.MapGet("/dashboard/summary", async (BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            if (tenantId is null) return Results.Forbid();
            var today = new DateTimeOffset(DateTime.UtcNow.Date, TimeSpan.Zero);
            var tables = await dbContext.Tables.AsNoTracking().Where(t => t.TenantId == tenantId && t.IsActive).ToListAsync(ct);
            var endedToday = await dbContext.MatchHistories.AsNoTracking()
                .Where(h => h.TenantId == tenantId && h.EndedAt != null && h.EndedAt >= today).ToListAsync(ct);
            var salesByGame = endedToday.Sum(h => h.TableTotal);
            var salesByConsumption = endedToday.Sum(h => h.ConsumptionTotal);
            return Results.Ok(new DashboardSummaryResponse(tables.Count,
                tables.Count(t => t.Status == BilliardTableStatus.Available),
                tables.Count(t => t.Status != BilliardTableStatus.Available),
                salesByGame + salesByConsumption, salesByGame, salesByConsumption));
        }).RequireAuthorization("AdminSession");

        api.MapGet("/dashboard/top-products", async (BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var start = new DateTimeOffset(DateTime.UtcNow.Date.AddDays(-7), TimeSpan.Zero);
            var rows = await dbContext.MatchConsumptions.AsNoTracking()
                .Where(i => i.CreatedAt >= start && i.MatchHistory != null && i.MatchHistory.TenantId == tenantId)
                .GroupBy(i => i.ProductNameSnapshot)
                .Select(g => new { Name = g.Key, Quantity = g.Sum(i => i.Quantity), Total = g.Sum(i => i.UnitPriceSnapshot * i.Quantity) })
                .OrderByDescending(g => g.Quantity).Take(10).ToListAsync(ct);
            return Results.Ok(rows.Select(r => new TopProductResponse(r.Name, r.Quantity, r.Total)).ToArray());
        }).RequireAuthorization("AdminSession");

        api.MapGet("/audit/logs", async (BilliardDbContext dbContext, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var tenantId = user.GetTenantId();
            var logs = await dbContext.AuditLogs.AsNoTracking()
                .Where(l => l.TenantId == tenantId)
                .OrderByDescending(l => l.CreatedAt).Take(200)
                .Select(l => new AuditLogResponse(l.Id, l.ActionType.ToString(), l.Description, l.UserId, l.TableId, l.MatchId, l.TransactionId, l.CreatedAt))
                .ToListAsync(ct);
            return Results.Ok(logs);
        }).RequireAuthorization("AdminSession");

        return app;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string GenerateRecoveryCode()
    {
        var bytes = RandomNumberGenerator.GetBytes(4);
        var num = BitConverter.ToUInt32(bytes);
        return (num % 100000000).ToString("D8");
    }

    private static async Task<(string accessToken, string refreshToken)> CreateTokenPairAsync(
        BilliardDbContext dbContext, IConfiguration config, User user, Guid? tenantId)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            config["Jwt:Key"] ?? "dev-only-change-in-production-use-openssl-rand-base64-48"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("sub", user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new("unique_name", user.UserName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("role", user.Role.ToString()),
        };
        if (tenantId.HasValue) claims.Add(new Claim("tenant", tenantId.Value.ToString()));

        var accessToken = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        var rawRefresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var session = new AdminSession(HashToken(rawRefresh), DateTimeOffset.UtcNow.AddDays(30), user.Id, tenantId);
        dbContext.Sessions.Add(session);

        return (new JwtSecurityTokenHandler().WriteToken(accessToken), rawRefresh);
    }

    private static async Task<TableDetailResponse> ToTableDetailAsync(BilliardDbContext dbContext, BilliardTable table, CancellationToken ct)
    {
        var match = table.ActiveMatchId is { } matchId
            ? await dbContext.MatchHistories.AsNoTracking().Include(h => h.Consumptions).FirstOrDefaultAsync(h => h.Id == matchId, ct)
            : null;
        return new TableDetailResponse(table.Id, table.Name, table.Code, table.Status.ToString(),
            table.HourlyRate, table.IsActive, table.ActiveMatchId,
            match is null ? null : ToMatchDetailResponse(match));
    }

    private static async Task<BilliardTable?> FindTableAsync(BilliardDbContext dbContext, string identifier, CancellationToken ct)
    {
        if (Guid.TryParse(identifier, out var id))
            return await dbContext.Tables.FirstOrDefaultAsync(t => t.Id == id, ct);
        var code = identifier.Trim().ToUpperInvariant();
        return await dbContext.Tables.FirstOrDefaultAsync(t => t.Code == code, ct);
    }

    private static async Task<string> NextTableCodeAsync(BilliardDbContext dbContext, Guid tenantId, CancellationToken ct)
    {
        var codes = await dbContext.Tables.AsNoTracking().Where(t => t.TenantId == tenantId).Select(t => t.Code).ToListAsync(ct);
        var used = new HashSet<string>(codes);
        var n = 1;
        while (used.Contains($"M{n}")) n++;
        return $"M{n}";
    }

    private static async Task<decimal> GetGlobalRateAsync(BilliardDbContext dbContext, Guid tenantId, CancellationToken ct)
    {
        var setting = await dbContext.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Key == "HourlyRate", ct);
        if (setting is not null && decimal.TryParse(setting.Value, out var rate) && rate > 0) return rate;
        var anyTable = await dbContext.Tables.AsNoTracking().FirstOrDefaultAsync(t => t.TenantId == tenantId, ct);
        return anyTable?.HourlyRate ?? 12000m;
    }

    private static async Task<bool> IsIdempotentAsync(BilliardDbContext dbContext, Guid? transactionId, CancellationToken ct)
        => transactionId is not null && await dbContext.AuditLogs.AnyAsync(l => l.TransactionId == transactionId, ct);

    private static async Task WriteAuditAsync(BilliardDbContext dbContext, AuditActionType actionType,
        Guid? userId, Guid? tableId, Guid? matchId, Guid? transactionId, string description,
        Guid? tenantId, CancellationToken ct)
    {
        dbContext.AuditLogs.Add(new AuditLog(actionType, description, userId, tableId, matchId, transactionId, tenantId));
        await dbContext.SaveChangesAsync(ct);
    }

    private static MatchDetailResponse ToMatchDetailResponse(MatchHistory match) => new(
        match.Id, match.WhitePlayerName, match.YellowPlayerName,
        match.WhiteScore, match.YellowScore, match.GameMode.ToString(),
        match.StartedAt, match.EndedAt is { } endedAt ? endedAt - match.StartedAt : TimeSpan.Zero,
        match.ConsumptionTotal,
        match.Consumptions.Select(c => new ConsumptionAmountResponse(
            c.Id, c.ProductNameSnapshot, c.UnitPriceSnapshot, c.Quantity, c.Total, c.CreatedAt)).ToArray());
}

#region Request / Response DTOs

public sealed record TableResponse(Guid Id, string Name, string Code, string Status, decimal HourlyRate, bool IsActive, Guid? ActiveMatchId);
public sealed record ProductCategoryResponse(Guid Id, string Name, IReadOnlyCollection<ProductResponse> Products);
public sealed record ProductResponse(Guid Id, string Name, decimal Price);
public sealed record DashboardSummaryResponse(int TotalTables, int AvailableTables, int OccupiedTables, decimal SalesToday, decimal SalesByGame, decimal SalesByConsumption);
public sealed record MatchDetailResponse(Guid Id, string WhitePlayerName, string YellowPlayerName, int WhiteScore, int YellowScore, string GameMode, DateTimeOffset StartedAt, TimeSpan Elapsed, decimal ConsumptionTotal, IReadOnlyCollection<ConsumptionAmountResponse> Consumptions);
public sealed record TableDetailResponse(Guid Id, string Name, string Code, string Status, decimal HourlyRate, bool IsActive, Guid? ActiveMatchId, MatchDetailResponse? ActiveMatch);

public sealed record LoginRequest(string? UserName, string? Password);
public sealed record LoginResponse(string AccessToken, string RefreshToken, string UserName, string Role, string? TenantName, string? TenantSlug, bool MustChangePassword);
public sealed record RefreshRequest(string? RefreshToken);
public sealed record ChangePasswordRequest(Guid UserId, string? CurrentPassword, string? NewPassword);
public sealed record ForgotPasswordRequest(string? UserName);
public sealed record ResetPasswordRequest(string? UserName, string? Code, string? NewPassword);
public sealed record CreateTableRequest(string Name, decimal HourlyRate, string? Code = null);
public sealed record UpdateTableRequest(string? Name, decimal HourlyRate, string? Code = null);
public sealed record UpdateAllRatesRequest(decimal HourlyRate);
public sealed record StartSessionRequest(string WhitePlayerName, string YellowPlayerName, GameMode GameMode, Guid? TransactionId, Guid? UserId, string? ConsumptionProduct = null, int ConsumptionQuantity = 0);
public sealed record ScoreRequest(string PlayerColor, int Delta, Guid? TransactionId, Guid? UserId);
public sealed record RenamePlayersRequest(string WhitePlayerName, string YellowPlayerName, Guid? TransactionId, Guid? UserId);
public sealed record AddConsumptionRequest(Guid ProductId, int Quantity, Guid? TransactionId, Guid? UserId);
public sealed record UpdateConsumptionRequest(int Quantity, Guid? TransactionId);
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
public sealed record MatchListItemResponse(Guid Id, Guid TableId, string WhitePlayerName, string YellowPlayerName, int WhiteScore, int YellowScore, int TotalCarambolas, string GameMode, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, decimal GrandTotal);
public sealed record TopProductResponse(string Name, int Quantity, decimal Total);
public sealed record AuditLogResponse(Guid Id, string ActionType, string Description, Guid? UserId, Guid? TableId, Guid? MatchId, Guid? TransactionId, DateTimeOffset CreatedAt);
public sealed record LocalResponse(Guid Id, string Name, string Slug, bool IsActive, int TableCount, int UserCount);
public sealed record CreateLocalResponse(Guid Id, string Name, string Slug, string DefaultPassword);
public sealed record CreateLocalRequest(string Name, string? InitialPassword = null);
public sealed record RecoveryCodeResponse(Guid Id, string TenantName, string UserName, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

#endregion
