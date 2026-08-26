namespace BuildingBlocks.Kernel.Domain.Specifications;

/// <summary>
/// Encapsulates a domain query (criteria + includes + ordering + paging) so the
/// "what" lives in the domain and the "how" (EF translation) in infrastructure.
/// </summary>
public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }

    IReadOnlyList<Expression<Func<T, object>>> Includes { get; }

    Expression<Func<T, object>>? OrderBy { get; }

    Expression<Func<T, object>>? OrderByDescending { get; }

    int? Skip { get; }

    int? Take { get; }

    /// <summary>When true the evaluator reads without change tracking (queries/read models).</summary>
    bool AsNoTracking { get; }

    /// <summary>Evaluates the criteria in memory (useful in tests and invariants).</summary>
    bool IsSatisfiedBy(T candidate);
}
