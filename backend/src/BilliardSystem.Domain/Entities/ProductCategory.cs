using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class ProductCategory : Entity
{
    private readonly List<Product> _products = [];

    private ProductCategory()
    {
    }

    public ProductCategory(string name, Guid tenantId, int sortOrder = 0)
    {
        Name = name;
        TenantId = tenantId;
        SortOrder = sortOrder;
    }

    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public Guid TenantId { get; private set; }
    public Tenant? Tenant { get; private set; }
    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();
}
