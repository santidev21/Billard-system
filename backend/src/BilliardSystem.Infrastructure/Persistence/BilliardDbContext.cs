using BilliardSystem.Application.Abstractions;
using BilliardSystem.Domain.Common;
using BilliardSystem.Domain.Entities;
using BilliardSystem.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BilliardSystem.Infrastructure.Persistence;

public sealed class DateTimeOffsetToTicksConverter : ValueConverter<DateTimeOffset, long>
{
    public DateTimeOffsetToTicksConverter()
        : base(
            value => value.UtcTicks,
            value => new DateTimeOffset(value, TimeSpan.Zero))
    {
    }
}

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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureDateTimeOffsetAsTicks(modelBuilder);

        modelBuilder.Entity<BilliardTable>(builder =>
        {
            builder.ToTable("Tables");
            builder.HasKey(table => table.Id);
            builder.Property(table => table.Name).HasMaxLength(80).IsRequired();
            builder.Property(table => table.Code).HasMaxLength(20).IsRequired();
            builder.HasIndex(table => table.Code).IsUnique();
            builder.Property(table => table.HourlyRate).HasPrecision(10, 2);
            builder.Property(table => table.Status).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<User>(builder =>
        {
            builder.ToTable("Users");
            builder.HasKey(user => user.Id);
            builder.Property(user => user.DisplayName).HasMaxLength(120).IsRequired();
            builder.Property(user => user.UserName).HasMaxLength(80).IsRequired();
            builder.HasIndex(user => user.UserName).IsUnique();
            builder.Property(user => user.PasswordHash).HasMaxLength(256).IsRequired();
            builder.Property(user => user.Role).HasConversion<string>().HasMaxLength(40);
        });

        modelBuilder.Entity<AuditLog>(builder =>
        {
            builder.ToTable("AuditLogs");
            builder.HasKey(log => log.Id);
            builder.Property(log => log.ActionType).HasConversion<string>().HasMaxLength(60);
            builder.Property(log => log.Description).HasMaxLength(500).IsRequired();
            builder.HasIndex(log => log.CreatedAt);
            builder.HasIndex(log => log.TransactionId).IsUnique().HasFilter("[TransactionId] IS NOT NULL");
        });

        modelBuilder.Entity<AppSetting>(builder =>
        {
            builder.ToTable("Settings");
            builder.HasKey(setting => setting.Id);
            builder.Property(setting => setting.Key).HasMaxLength(120).IsRequired();
            builder.Property(setting => setting.Value).HasMaxLength(1000).IsRequired();
            builder.HasIndex(setting => setting.Key).IsUnique();
        });

        modelBuilder.Entity<ProductCategory>(builder =>
        {
            builder.ToTable("Categories");
            builder.HasKey(category => category.Id);
            builder.Property(category => category.Name).HasMaxLength(100).IsRequired();
            builder.HasMany(category => category.Products)
                .WithOne(product => product.Category)
                .HasForeignKey(product => product.CategoryId);
        });

        modelBuilder.Entity<Product>(builder =>
        {
            builder.ToTable("Products");
            builder.HasKey(product => product.Id);
            builder.Property(product => product.Name).HasMaxLength(120).IsRequired();
            builder.Property(product => product.Price).HasPrecision(10, 2);
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
            builder.HasOne(match => match.Table)
                .WithMany()
                .HasForeignKey(match => match.TableId);
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
            builder.HasOne(consumption => consumption.Product)
                .WithMany()
                .HasForeignKey(consumption => consumption.ProductId);
        });

        modelBuilder.Entity<AdminSession>(builder =>
        {
            builder.ToTable("Sessions");
            builder.HasKey(session => session.Id);
            builder.Property(session => session.TokenHash).HasMaxLength(256).IsRequired();
            builder.HasIndex(session => session.TokenHash);
            builder.HasIndex(session => session.ExpiresAt);
        });

        Seed(modelBuilder);
    }

    private static void ConfigureDateTimeOffsetAsTicks(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                {
                    var nullable = property.ClrType == typeof(DateTimeOffset?);
                    var converterType = typeof(DateTimeOffsetToTicksConverter);
                    property.SetValueConverter((ValueConverter)Activator.CreateInstance(converterType)!);
                }
            }
        }
    }

    private static void Seed(ModelBuilder modelBuilder)
    {
        var table1 = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var drinks = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var water = Guid.Parse("30000000-0000-0000-0000-000000000001");

        modelBuilder.Entity<BilliardTable>().HasData(
            new { Id = table1, Name = "Mesa 1", Code = "M1", Status = BilliardTableStatus.Available, HourlyRate = 12000m, IsActive = true, ActiveMatchId = (Guid?)null });

        modelBuilder.Entity<ProductCategory>().HasData(
            new { Id = drinks, Name = "Bebidas", SortOrder = 1, IsActive = true });

        modelBuilder.Entity<Product>().HasData(
            new { Id = water, CategoryId = drinks, Name = "Agua", Price = 3000m, IsActive = true });

        modelBuilder.Entity<AppSetting>().HasData(
            new { Id = Guid.Parse("40000000-0000-0000-0000-000000000001"), Key = "ReplayBufferSeconds", Value = "60", UpdatedAt = DateTimeOffset.UnixEpoch },
            new { Id = Guid.Parse("40000000-0000-0000-0000-000000000002"), Key = "BusinessName", Value = "Billar Tres Bandas", UpdatedAt = DateTimeOffset.UnixEpoch },
            new { Id = Guid.Parse("40000000-0000-0000-0000-000000000003"), Key = "AdminPassword", Value = "8C6976E5B5410415BDE908BD4DEE15DFB167A9C873FC4BB8A81F6F2AB448A918", UpdatedAt = DateTimeOffset.UnixEpoch },
            new { Id = Guid.Parse("40000000-0000-0000-0000-000000000004"), Key = "HourlyRate", Value = "12000", UpdatedAt = DateTimeOffset.UnixEpoch });
    }
}
