using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class ProductCategory : Entity
{
    private readonly List<Product> _products = [];

    private ProductCategory()
    {
    }

    public ProductCategory(string name, int sortOrder = 0)
    {
        Name = name;
        SortOrder = sortOrder;
    }

    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<Product> Products => _products.AsReadOnly();
}
