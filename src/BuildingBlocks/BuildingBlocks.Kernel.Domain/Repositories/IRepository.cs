namespace BuildingBlocks.Kernel.Domain.Repositories;

/// <summary>
/// Persistence-agnostic contract for aggregate roots.
/// One repository per aggregate; queries that cross aggregates belong to read models.
/// Domain queries are expressed as <see cref="ISpecification{T}"/>.
/// </summary>
public interface IRepository<TAggregate, in TId>
    where TAggregate : class, IAggregateRoot
    where TId : notnull
{
    Task<TAggregate?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<TAggregate?> FirstOrDefaultAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TAggregate>> ListAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default);

    Task<int> CountAsync(ISpecification<TAggregate> specification, CancellationToken cancellationToken = default);

    Task AddAsync(TAggregate aggregate, CancellationToken cancellationToken = default);

    void Update(TAggregate aggregate);

    void Remove(TAggregate aggregate);
}
