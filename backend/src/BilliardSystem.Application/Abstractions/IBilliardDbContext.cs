using BilliardSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace BilliardSystem.Application.Abstractions;

public interface IBilliardDbContext
{
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AppSetting> Settings { get; }
    DbSet<BilliardTable> Tables { get; }
    DbSet<MatchConsumption> MatchConsumptions { get; }
    DbSet<MatchHistory> MatchHistories { get; }
    DbSet<MatchRound> MatchRounds { get; }
    DbSet<MatchScoreLog> MatchScoreLogs { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductCategory> Categories { get; }
    DbSet<User> Users { get; }
    DbSet<AdminSession> Sessions { get; }
    DbSet<Tenant> Tenants { get; }
    DbSet<RecoveryRequest> RecoveryRequests { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
