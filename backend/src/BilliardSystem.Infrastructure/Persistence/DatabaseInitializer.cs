using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BilliardSystem.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    private static int _codeCounter = 0;

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BilliardDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await UpgradeLegacySchemaAsync(dbContext);
    }

    private static async Task UpgradeLegacySchemaAsync(BilliardDbContext dbContext)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            var wasOpen = connection.State == System.Data.ConnectionState.Open;
            if (!wasOpen)
            {
                await connection.OpenAsync();
            }

            var hasCode = await HasColumnAsync(connection, "Tables", "Code");
            var hasIsActive = await HasColumnAsync(connection, "Tables", "IsActive");

            if (!hasCode)
            {
                await using var alter1 = connection.CreateCommand();
                alter1.CommandText = "ALTER TABLE 'Tables' ADD COLUMN 'Code' TEXT NOT NULL DEFAULT '';";
                await alter1.ExecuteNonQueryAsync();
            }

            if (!hasIsActive)
            {
                await using var alter2 = connection.CreateCommand();
                alter2.CommandText = "ALTER TABLE 'Tables' ADD COLUMN 'IsActive' INTEGER NOT NULL DEFAULT 1;";
                await alter2.ExecuteNonQueryAsync();
            }

            if (!(await HasColumnAsync(connection, "MatchRounds", "StartedAt")))
            {
                try
                {
                    await using var alter3 = connection.CreateCommand();
                    alter3.CommandText = "ALTER TABLE 'MatchRounds' ADD COLUMN 'StartedAt' BIGINT NOT NULL DEFAULT 0;";
                    await alter3.ExecuteNonQueryAsync();
                }
                catch
                {
                    // La tabla no existe aún; EnsureCreated la crea con el esquema nuevo.
                }
            }

            try
            {
                await using var backfill = connection.CreateCommand();
                backfill.CommandText = @"
UPDATE 'MatchRounds'
SET 'StartedAt' = (SELECT 'StartedAt' FROM 'MatchHistories' WHERE 'MatchHistories'.'Id' = 'MatchRounds'.'MatchHistoryId')
WHERE 'StartedAt' = 0
  AND 'RoundNumber' = 1;";
                await backfill.ExecuteNonQueryAsync();
            }
            catch
            {
                // Sin backfill disponible (columna nueva sin datos previos).
            }

            if (!wasOpen)
            {
                await connection.CloseAsync();
            }

            var tables = await dbContext.Tables.ToListAsync();
            var changed = false;
            foreach (var table in tables)
            {
                if (string.IsNullOrWhiteSpace(table.Code))
                {
                    var used = tables.Where(t => !ReferenceEquals(t, table)).Select(t => t.Code).ToHashSet();
                    while (used.Contains($"M{++_codeCounter}"))
                    {
                    }

                    table.SetCode($"M{_codeCounter}");
                    changed = true;
                }
            }

            if (changed)
            {
                await dbContext.SaveChangesAsync();
            }
        }
        catch
        {
            // Esquema legado inexistente o ya migrado; la app sigue con EnsureCreated.
        }
    }

    private static async Task<bool> HasColumnAsync(System.Data.Common.DbConnection connection, string table, string column)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = '{column}';";
        var result = await command.ExecuteScalarAsync();
        return result is long l && l > 0;
    }
}
