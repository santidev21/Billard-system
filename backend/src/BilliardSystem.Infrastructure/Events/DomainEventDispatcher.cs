using BilliardSystem.Application.Abstractions;
using BilliardSystem.Domain.Common;
using Microsoft.Extensions.DependencyInjection;

namespace BilliardSystem.Infrastructure.Events;

public sealed class DomainEventDispatcher(IServiceProvider serviceProvider) : IDomainEventDispatcher
{
    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = serviceProvider.GetServices(handlerType);

            foreach (var handler in handlers)
            {
                var handleMethod = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync));
                if (handleMethod?.Invoke(handler, [domainEvent, cancellationToken]) is Task task)
                {
                    await task;
                }
            }
        }
    }
}
