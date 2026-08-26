using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Events;
using BuildingBlocks.Kernel.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.Kernel.Infrastructure.Persistence;

/// <summary>
/// DbContext base that implements <see cref="IUnitOfWork"/> and dispatches the
/// domain events raised by tracked aggregates as part of SaveChanges.
/// Handlers run before the final commit, so their changes join the same transaction.
/// </summary>
public abstract class AppDbContextBase(DbContextOptions options, IDomainEventDispatcher domainEventDispatcher)
    : DbContext(options), IUnitOfWork
{
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await DispatchDomainEventsAsync(cancellationToken).ConfigureAwait(false);
        return await base.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DispatchDomainEventsAsync(CancellationToken cancellationToken)
    {
        // Handlers may raise further events on other aggregates; loop until drained.
        while (true)
        {
            var domainEvents = ChangeTracker.Entries<IAggregateRoot>()
                .Select(entry => entry.Entity)
                .Where(aggregate => aggregate.DomainEvents.Count > 0)
                .SelectMany(aggregate =>
                {
                    var events = aggregate.DomainEvents.ToArray();
                    aggregate.ClearDomainEvents();
                    return events;
                })
                .ToArray();

            if (domainEvents.Length == 0)
            {
                return;
            }

            await domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken).ConfigureAwait(false);
        }
    }
}
