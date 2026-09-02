using BilliardSystem.Application.Abstractions;
using BilliardSystem.Infrastructure.Events;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace BilliardSystem.Infrastructure;

public sealed class DesignTimeBilliardDbContextFactory : IDesignTimeDbContextFactory<BilliardDbContext>
{
    public BilliardDbContext CreateDbContext(string[] args)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDomainEventDispatcher, DomainEventDispatcher>();
        var provider = services.BuildServiceProvider();

        var options = new DbContextOptionsBuilder<BilliardDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=billiard;Username=postgres;Password=postgres")
            .Options;

        return new BilliardDbContext(options, provider.GetRequiredService<IDomainEventDispatcher>());
    }
}
