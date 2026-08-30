using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Seeder;

public sealed class NullDispatcher : IDomainEventDispatcher
{
    public Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
