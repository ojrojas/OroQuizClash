namespace BuildingBlocks.Kernel.Domain.Repositories;

/// <summary>
/// Commits all pending changes of the current business transaction atomically,
/// dispatching domain events raised by the touched aggregates.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}