using BilliardSystem.Domain.Enums;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

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

        api.MapGet("/settings", async (BilliardDbContext dbContext, CancellationToken cancellationToken) =>
        {
            var settings = await dbContext.Settings
                .AsNoTracking()
                .OrderBy(setting => setting.Key)
                .ToDictionaryAsync(setting => setting.Key, setting => setting.Value, cancellationToken);

            return Results.Ok(settings);
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

        return app;
    }
}

public sealed record TableResponse(Guid Id, string Name, string Status, decimal HourlyRate, Guid? ActiveMatchId);

public sealed record ProductCategoryResponse(Guid Id, string Name, IReadOnlyCollection<ProductResponse> Products);

public sealed record ProductResponse(Guid Id, string Name, decimal Price);

public sealed record DashboardSummaryResponse(int TotalTables, int AvailableTables, int OccupiedTables, decimal SalesToday);
