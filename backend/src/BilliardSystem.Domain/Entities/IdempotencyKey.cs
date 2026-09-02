using BilliardSystem.Domain.Common;

namespace BilliardSystem.Domain.Entities;

public sealed class IdempotencyKey : Entity
{
    private IdempotencyKey()
    {
    }

    public IdempotencyKey(Guid transactionId)
    {
        Id = transactionId;
        TransactionId = transactionId;
    }

    public Guid TransactionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
}
