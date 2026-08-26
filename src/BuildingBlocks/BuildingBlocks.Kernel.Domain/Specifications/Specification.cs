namespace BuildingBlocks.Kernel.Domain.Specifications;

/// <summary>
/// Base class for specifications. Build criteria in the constructor:
/// <code>
/// public sealed class OverdueOrdersSpecification : Specification&lt;Order&gt;
/// {
///     public OverdueOrdersSpecification(DateTime now) : base(o => o.DueDateUtc &lt; now) { }
/// }
/// </code>
/// Specifications compose with <see cref="And"/>, <see cref="Or"/> and <see cref="Not"/>.
/// </summary>
public abstract class Specification<T> : ISpecification<T>
{
    private readonly List<Expression<Func<T, object>>> _includes = [];
    private Func<T, bool>? _compiledCriteria;

    protected Specification()
    {
    }

    protected Specification(Expression<Func<T, bool>> criteria) => Criteria = criteria;

    public Expression<Func<T, bool>>? Criteria { get; private set; }

    public IReadOnlyList<Expression<Func<T, object>>> Includes => _includes;

    public Expression<Func<T, object>>? OrderBy { get; private set; }

    public Expression<Func<T, object>>? OrderByDescending { get; private set; }

    public int? Skip { get; private set; }

    public int? Take { get; private set; }

    public bool AsNoTracking { get; private set; }

    public bool IsSatisfiedBy(T candidate)
    {
        if (Criteria is null)
        {
            return true;
        }

        _compiledCriteria ??= Criteria.Compile();
        return _compiledCriteria(candidate);
    }

    /// <summary>Adds a criteria; multiple calls are combined with AND.</summary>
    protected void Where(Expression<Func<T, bool>> criteria)
    {
        Criteria = Criteria is null ? criteria : Criteria.AndAlso(criteria);
        _compiledCriteria = null;
    }

    protected void AddInclude(Expression<Func<T, object>> include) => _includes.Add(include);

    protected void ApplyOrderBy(Expression<Func<T, object>> orderBy) => OrderBy = orderBy;

    protected void ApplyOrderByDescending(Expression<Func<T, object>> orderByDescending) =>
        OrderByDescending = orderByDescending;

    protected void ApplyPaging(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }

    protected void ApplyAsNoTracking() => AsNoTracking = true;

    public Specification<T> And(ISpecification<T> other) =>
        new CombinedSpecification<T>(Compose(other, static (left, right) => left.AndAlso(right)));

    public Specification<T> Or(ISpecification<T> other) =>
        new CombinedSpecification<T>(Compose(other, static (left, right) => left.OrElse(right)));

    public Specification<T> Not() =>
        new CombinedSpecification<T>(Criteria is null
            ? static _ => false
            : Expression.Lambda<Func<T, bool>>(Expression.Not(Criteria.Body), Criteria.Parameters));

    private Expression<Func<T, bool>> Compose(
        ISpecification<T> other,
        Func<Expression<Func<T, bool>>, Expression<Func<T, bool>>, Expression<Func<T, bool>>> combine)
    {
        if (Criteria is null)
        {
            return other.Criteria ?? (static _ => true);
        }

        return other.Criteria is null ? Criteria : combine(Criteria, other.Criteria);
    }
}

internal sealed class CombinedSpecification<T> : Specification<T>
{
    public CombinedSpecification(Expression<Func<T, bool>> criteria) : base(criteria)
    {
    }
}

/// <summary>
/// Combines lambda expressions rebinding parameters, so the result stays
/// translatable by LINQ providers such as EF Core.
/// </summary>
public static class ExpressionExtensions
{
    public static Expression<Func<T, bool>> AndAlso<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right) => Combine(left, right, Expression.AndAlso);

    public static Expression<Func<T, bool>> OrElse<T>(
        this Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right) => Combine(left, right, Expression.OrElse);

    private static Expression<Func<T, bool>> Combine<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right,
        Func<Expression, Expression, BinaryExpression> merge)
    {
        var parameter = left.Parameters[0];
        var rightBody = new ParameterReplacer(right.Parameters[0], parameter).Visit(right.Body);
        return Expression.Lambda<Func<T, bool>>(merge(left.Body, rightBody), parameter);
    }

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}