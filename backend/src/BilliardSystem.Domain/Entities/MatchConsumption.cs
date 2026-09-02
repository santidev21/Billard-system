using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class MatchConsumption : Entity
{
    private MatchConsumption()
    {
    }

    public MatchConsumption(Guid matchHistoryId, Guid productId, string productNameSnapshot, decimal unitPriceSnapshot, int quantity)
    {
        MatchHistoryId = matchHistoryId;
        ProductId = productId;
        ProductNameSnapshot = productNameSnapshot;
        UnitPriceSnapshot = unitPriceSnapshot;
        Quantity = quantity;
    }

    public Guid MatchHistoryId { get; private set; }
    public MatchHistory? MatchHistory { get; private set; }
    public Guid ProductId { get; private set; }
    public Product? Product { get; private set; }
    public string ProductNameSnapshot { get; private set; } = string.Empty;
    public decimal UnitPriceSnapshot { get; private set; }
    public int Quantity { get; private set; }
    public decimal Total => UnitPriceSnapshot * Quantity;
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public void SetQuantity(int quantity)
    {
        if (quantity < 1 || quantity > 999)
            throw new ArgumentException("Quantity must be between 1 and 999.");
        Quantity = quantity;
    }
}
