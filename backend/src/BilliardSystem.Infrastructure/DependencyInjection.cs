using BilliardSystem.Application.Abstractions;
using BilliardSystem.Infrastructure.Events;
using BilliardSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BilliardSystem.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("BilliardDatabase")
            ?? "Data Source=billiard-system.db";

        services.AddDbContext<BilliardDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<IBilliardDbContext>(provider => provider.GetRequiredService<BilliardDbContext>());
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }
}
