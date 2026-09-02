using BilliardSystem.Application.Abstractions;
using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace BilliardSystem.Infrastructure.Persistence;

public sealed class BilliardDbContext : DbContext, IBilliardDbContext
{
    private readonly IDomainEventDispatcher _domainEventDispatcher;

    public BilliardDbContext(DbContextOptions<BilliardDbContext> options, IDomainEventDispatcher domainEventDispatcher)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<BilliardTable> Tables => Set<BilliardTable>();
    public DbSet<MatchConsumption> MatchConsumptions => Set<MatchConsumption>();
    public DbSet<MatchHistory> MatchHistories => Set<MatchHistory>();
    public DbSet<MatchRound> MatchRounds => Set<MatchRound>();
    public DbSet<MatchScoreLog> MatchScoreLogs => Set<MatchScoreLog>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductCategory> Categories => Set<ProductCategory>();
    public DbSet<User> Users => Set<User>();
    public DbSet<AdminSession> Sessions => Set<AdminSession>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<RecoveryRequest> RecoveryRequests => Set<RecoveryRequest>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEvents = ChangeTracker.Entries<Entity>()
            .SelectMany(entry => entry.Entity.DomainEvents)
            .ToArray();

        var result = await base.SaveChangesAsync(cancellationToken);

        if (domainEvents.Length > 0)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
            foreach (var entity in ChangeTracker.Entries<Entity>())
            {
                entity.Entity.ClearDomainEvents();
            }
        }

        return result;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(builder =>
        {
            builder.ToTable("Tenants");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Name).HasMaxLength(120).IsRequired();
            builder.Property(t => t.Slug).HasMaxLength(120).IsRequired();
            builder.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<BilliardTable>(builder =>
        {
            builder.ToTable("Tables");
            builder.HasKey(table => table.Id);
            builder.Property(table => table.Name).HasMaxLength(80).IsRequired();
            builder.Property(table => table.Code).HasMaxLength(20).IsRequired();
            builder.HasIndex(table => new { table.TenantId, table.Code }).IsUnique();
            builder.Property(table => table.HourlyRate).HasPrecision(10, 2);
            builder.Property(table => table.Status).HasConversion<string>().HasMaxLength(40);
            builder.HasOne(table => table.Tenant).WithMany().HasForeignKey(table => table.TenantId);
        });

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(user => user.Id);
            builder.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
            builder.Property(user => user.UserName).HasMaxLength(80).IsRequired();
            builder.HasIndex(user => new { user.TenantId, user.UserName }).IsUnique().HasFilter("\"TenantId\" IS NOT NULL");
            builder.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
            builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(40);
            builder.Property(user => user.Email).HasMaxLength(200);
            builder.HasOne(user => user.Tenant).WithMany().HasForeignKey(user => user.TenantId);
        });

        modelBuilder.Entity<AdminSession>(builder =>
        {
            builder.ToTable("Sessions");
            builder.HasKey(session => session.Id);
            builder.Property(session => session.TokenHash).HasMaxLength(256).IsRequired();
            builder.HasIndex(session => session.TokenHash);
            builder.HasIndex(session => session.ExpiresAt);
            builder.HasOne(session => session.User).WithMany().HasForeignKey(session => session.UserId);
            builder.HasOne(session => session.Tenant).WithMany().HasForeignKey(session => session.TenantId);
        });

        modelBuilder.Entity<RecoveryRequest>(builder =>
        {
            builder.ToTable("RecoveryRequests");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.CodeHash).HasMaxLength(256).IsRequired();
            builder.HasIndex(r => r.TenantId);
            builder.HasOne(r => r.Tenant).WithMany().HasForeignKey(r => r.TenantId);
            builder.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId);
        });

        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(log => log.Id);
            builder.Property(log => log.ActionType).HasConversion<string>().HasMaxLength(60);
            builder.Property(log => log.Description).HasMaxLength(500).IsRequired();
            builder.HasIndex(log => log.CreatedAt);
            builder.HasIndex(log => log.TransactionId).IsUnique().HasFilter("\"TransactionId\" IS NOT NULL");
            builder.HasOne(log => log.Tenant).WithMany().HasForeignKey(log => log.TenantId);
        });

        modelBuilder.Entity<AppSetting>(builder =>
        {
            builder.ToTable("Settings");
            builder.HasKey(setting => setting.Id);
            builder.Property(setting => setting.Key).HasMaxLength(120).IsRequired();
            builder.Property(setting => setting.Value).HasMaxLength(1000).IsRequired();
            builder.HasIndex(setting => new { setting.TenantId, setting.Key }).IsUnique().HasFilter("\"TenantId\" IS NOT NULL");
            builder.HasOne(setting => setting.Tenant).WithMany().HasForeignKey(setting => setting.TenantId);
        });

        modelBuilder.Entity<ProductCategory>(builder =>
        {
            builder.ToTable("Categories");
            builder.HasKey(category => category.Id);
            builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
            builder.HasMany(category => category.Products)
                .WithOne(product => product.Category)
                .HasForeignKey(product => product.CategoryId);
            builder.HasOne(category => category.Tenant).WithMany().HasForeignKey(category => category.TenantId);
        });

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("Products");
            builder.HasKey(product => product.Id);
            builder.Property(product => product.Name).HasMaxLength(120).IsRequired();
            builder.Property(product => product.Price).HasPrecision(10, 2);
            builder.HasOne(product => product.Tenant).WithMany().HasForeignKey(product => product.TenantId);
        });

        modelBuilder.Entity<MatchHistory>(builder =>
        {
            builder.ToTable("MatchHistories");
            builder.HasKey(match => match.Id);
            builder.Property(match => match.WhitePlayerName).HasMaxLength(80).IsRequired();
            builder.Property(match => match.YellowPlayerName).HasMaxLength(80).IsRequired();
            builder.Property(match => match.HourlyRateSnapshot).HasPrecision(10, 2);
            builder.Property(match => match.TableTotal).HasPrecision(10, 2);
            builder.Property(match => match.ConsumptionTotal).HasPrecision(10, 2);
            builder.Property(match => match.GrandTotal).HasPrecision(10, 2);
            builder.Property(match => match.SystemVersion).HasMaxLength(40).IsRequired();
            builder.HasOne(match => match.Table).WithMany().HasForeignKey(match => match.TableId);
            builder.HasOne(match => match.Tenant).WithMany().HasForeignKey(match => match.TenantId);
            builder.Navigation(match => match.ScoreLogs).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(match => match.Consumptions).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(match => match.Rounds).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<MatchRound>(builder =>
        {
            builder.ToTable("MatchRounds");
            builder.HasKey(round => round.Id);
            builder.HasOne(round => round.MatchHistory)
                .WithMany(match => match.Rounds)
                .HasForeignKey(round => round.MatchHistoryId);
        });

        modelBuilder.Entity<MatchScoreLog>(builder =>
        {
            builder.ToTable("MatchScoreLogs");
            builder.HasKey(score => score.Id);
            builder.Property(score => score.PlayerColor).HasMaxLength(20).IsRequired();
            builder.HasOne(score => score.MatchHistory)
                .WithMany(match => match.ScoreLogs)
                .HasForeignKey(score => score.MatchHistoryId);
        });

        modelBuilder.Entity<MatchConsumption>(builder =>
        {
            builder.ToTable("MatchConsumptions");
            builder.HasKey(consumption => consumption.Id);
            builder.Property(consumption => consumption.ProductNameSnapshot).HasMaxLength(120).IsRequired();
            builder.Property(consumption => consumption.UnitPriceSnapshot).HasPrecision(10, 2);
            builder.Ignore(consumption => consumption.Total);
            builder.HasOne(consumption => consumption.MatchHistory)
                .WithMany(match => match.Consumptions)
                .HasForeignKey(consumption => consumption.MatchHistoryId);
            builder.HasOne(consumption => consumption.Product).WithMany().HasForeignKey(consumption => consumption.ProductId);
        });

        Seed(modelBuilder);
    }

    private static void Seed(ModelBuilder modelBuilder)
    {
        var seedDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var demoTenant = Guid.Parse("99999999-0000-0000-0000-000000000001");
        var superUser = Guid.Parse("99999999-0000-0000-0000-000000000099");
        var table1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var drinks = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var water = Guid.Parse("30000000-0000-0000-0000-000000000001");

        modelBuilder.Entity<Tenant>().HasData(
            new { Id = demoTenant, Name = "Demo", Slug = "demo", IsActive = true, CreatedAt = seedDate }
        );

        modelBuilder.Entity<User>().HasData(
            new
            {
                Id = superUser,
                DisplayName = "Super Admin",
                UserName = "superadmin",
                PasswordHash = PasswordHasher.Hash("SuperAdmin123!"),
                Role = UserRole.SuperAdmin,
                TenantId = (Guid?)null,
                Email = (string?)null,
                IsActive = true,
                CreatedAt = seedDate
            }
        );

        modelBuilder.Entity<BilliardTable>().HasData(
            new { Id = table1, TenantId = demoTenant, Name = "Mesa 1", Code = "M1", Status = BilliardTableStatus.Available, HourlyRate = 12000m, IsActive = true, ActiveMatchId = (Guid?)null }
        );

        modelBuilder.Entity<ProductCategory>().HasData(
            new { Id = drinks, TenantId = demoTenant, Name = "Bebidas", SortOrder = 1, IsActive = true }
        );

        modelBuilder.Entity<Product>().HasData(
            new { Id = water, CategoryId = drinks, TenantId = demoTenant, Name = "Agua", Price = 3000m, IsActive = true }
        );

        modelBuilder.Entity<AppSetting>().HasData(
            new { Id = Guid.Parse("40000000-0000-0000-0000-000000000001"), Key = "ReplayBufferSeconds", Value = "60", TenantId = (Guid?)null, UpdatedAt = seedDate },
            new { Id = Guid.Parse("40000000-0000-0000-0000-000000000002"), Key = "BusinessName", Value = "Billar Tres Bandas", TenantId = (Guid?)null, UpdatedAt = seedDate },
            new { Id = Guid.Parse("40000000-0000-0000-0000-000000000003"), Key = "AdminPassword", Value = PasswordHasher.Hash("admin"), TenantId = (Guid?)null, UpdatedAt = seedDate },
            new { Id = Guid.Parse("40000000-0000-0000-0000-000000000004"), Key = "HourlyRate", Value = "12000", TenantId = (Guid?)null, UpdatedAt = seedDate }
        );
    }
}
