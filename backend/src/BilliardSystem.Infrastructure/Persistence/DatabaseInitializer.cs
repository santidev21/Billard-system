using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BilliardSystem.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BilliardDbContext>();

        await dbContext.Database.MigrateAsync();

        await SeedSuperUserAsync(dbContext);
        await SeedDemoTenantAsync(dbContext);
    }

    private static async Task SeedSuperUserAsync(BilliardDbContext dbContext)
    {
        var superUserName = Environment.GetEnvironmentVariable("Super__UserName") ?? "superadmin";
        var superPassword = Environment.GetEnvironmentVariable("Super__Password") ?? "SuperAdmin123!";

        if (await dbContext.Users.AnyAsync(u => u.UserName == superUserName))
        {
            return;
        }

        var passwordHash = Domain.Common.PasswordHasher.Hash(superPassword);
        var user = new Domain.Entities.User(
            "Super Admin",
            superUserName,
            passwordHash,
            Domain.Enums.UserRole.SuperAdmin);

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
    }

    private static async Task SeedDemoTenantAsync(BilliardDbContext dbContext)
    {
        if (await dbContext.Tenants.AnyAsync(t => t.Slug == "demo"))
        {
            return;
        }

        var tenant = new Domain.Entities.Tenant("Demo");
        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        var table = new Domain.Entities.BilliardTable("Mesa 1", 12000m, tenant.Id);
        table.SetCode("M1");
        dbContext.Tables.Add(table);

        var category = new Domain.Entities.ProductCategory("Bebidas", tenant.Id);
        dbContext.Categories.Add(category);
        await dbContext.SaveChangesAsync();

        var product = new Domain.Entities.Product(category.Id, "Agua", 3000m, tenant.Id);
        dbContext.Products.Add(product);

        dbContext.Settings.Add(new Domain.Entities.AppSetting("HourlyRate", "12000", tenant.Id));
        dbContext.Settings.Add(new Domain.Entities.AppSetting("BusinessName", "Billar Tres Bandas", tenant.Id));

        await dbContext.SaveChangesAsync();
    }
}
