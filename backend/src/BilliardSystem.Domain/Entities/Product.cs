using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class Product : Entity
{
    private Product()
    {
    }

    public Product(Guid categoryId, string name, decimal price)
    {
        CategoryId = categoryId;
        Name = name;
        Price = price;
    }

    public Guid CategoryId { get; private set; }
    public ProductCategory? Category { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsActive { get; private set; } = true;
}
