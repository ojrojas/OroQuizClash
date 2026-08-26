using BuildingBlocks.Kernel.Domain.Rules;

namespace BuildingBlocks.Kernel.Domain.Entities;

/// <summary>
/// Base class for entities identified by <typeparamref name="TId"/>.
/// Equality is based on identity, not on attributes.
/// </summary>
public abstract class Entity<TId> : IEquatable<Entity<TId>>
    where TId : notnull
{
    protected Entity(TId id) => Id = id;

    /// <summary>EF Core / serializer constructor.</summary>
    protected Entity() => Id = default!;

    public TId Id { get; protected set; }

    /// <summary>
    /// Checks a business rule and throws <see cref="BusinessRuleValidationException"/> when broken.
    /// </summary>
    protected static void CheckRule(IBusinessRule rule)
    {
        if (rule.IsBroken())
        {
            throw new BusinessRuleValidationException(rule);
        }
    }

    public bool Equals(Entity<TId>? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return GetType() == other.GetType() && EqualityComparer<TId>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj) => Equals(obj as Entity<TId>);

    public override int GetHashCode() => HashCode.Combine(GetType(), Id);

    public static bool operator ==(Entity<TId>? left, Entity<TId>? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(Entity<TId>? left, Entity<TId>? right) => !(left == right);
}