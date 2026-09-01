using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class Product : Entity
{
    private Product()
    {
    }

    public Product(Guid categoryId, string name, decimal price, Guid tenantId)
    {
        CategoryId = categoryId;
        Name = name;
        Price = price;
        TenantId = tenantId;
    }

    public Guid CategoryId { get; private set; }
    public ProductCategory? Category { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }

    public void Update(string name, decimal price)
    {
        Name = name;
        Price = price;
    }

    public void Deactivate() => IsActive = false;
}
