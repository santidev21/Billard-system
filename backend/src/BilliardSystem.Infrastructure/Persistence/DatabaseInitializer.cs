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
        await SeedDefaultProductsForExistingTenantsAsync(dbContext);
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
        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(t => t.Slug == "demo");

        if (tenant is null)
        {
            tenant = new Domain.Entities.Tenant("Demo");
            dbContext.Tenants.Add(tenant);
            await dbContext.SaveChangesAsync();
        }

        if (!await dbContext.Tables.AnyAsync(t => t.TenantId == tenant.Id))
        {
            var table = new Domain.Entities.BilliardTable("Mesa 1", 12000m, tenant.Id);
            table.SetCode("M1");
            dbContext.Tables.Add(table);

            if (!await dbContext.Categories.AnyAsync(c => c.TenantId == tenant.Id))
            {
                var category = new Domain.Entities.ProductCategory("Bebidas", tenant.Id);
                dbContext.Categories.Add(category);
                await dbContext.SaveChangesAsync();

                var product = new Domain.Entities.Product(category.Id, "Agua", 3000m, tenant.Id);
                dbContext.Products.Add(product);
            }

            if (!await dbContext.Settings.AnyAsync(s => s.TenantId == tenant.Id))
            {
                dbContext.Settings.Add(new Domain.Entities.AppSetting("HourlyRate", "12000", tenant.Id));
                dbContext.Settings.Add(new Domain.Entities.AppSetting("BusinessName", "Billar Tres Bandas", tenant.Id));
            }

            await dbContext.SaveChangesAsync();
        }
    }

    private static async Task SeedDefaultProductsForExistingTenantsAsync(BilliardDbContext dbContext)
    {
        var tenants = await dbContext.Tenants.ToListAsync();
        foreach (var tenant in tenants)
        {
            if (tenant.Slug == "demo")
            {
                continue;
            }
            if (await dbContext.Categories.AnyAsync(c => c.TenantId == tenant.Id))
            {
                continue;
            }
            var category = new Domain.Entities.ProductCategory("Bebidas", tenant.Id);
            dbContext.Categories.Add(category);
            await dbContext.SaveChangesAsync();

            var product = new Domain.Entities.Product(category.Id, "Agua", 3000m, tenant.Id);
            dbContext.Products.Add(product);

            if (!await dbContext.Settings.AnyAsync(s => s.TenantId == tenant.Id && s.Key == "HourlyRate"))
            {
                dbContext.Settings.Add(new Domain.Entities.AppSetting("HourlyRate", "12000", tenant.Id));
            }
            if (!await dbContext.Settings.AnyAsync(s => s.TenantId == tenant.Id && s.Key == "BusinessName"))
            {
                dbContext.Settings.Add(new Domain.Entities.AppSetting("BusinessName", "Billar Tres Bandas", tenant.Id));
            }
            await dbContext.SaveChangesAsync();
        }
    }
}
