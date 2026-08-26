namespace BuildingBlocks.Kernel.Domain.ValueObjects;

/// <summary>
/// Base class for value objects. Equality is structural: two value objects
/// are equal when all their atomic values are equal.
/// Prefer C# records for simple cases; use this base when you need
/// custom equality components (e.g. collections).
/// </summary>
public abstract class ValueObject : IEquatable<ValueObject>
{
    /// <summary>Yields the values that define equality, in a stable order.</summary>
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public bool Equals(ValueObject? other)
    {
        if (other is null || other.GetType() != GetType())
        {
            return false;
        }

        return GetEqualityComponents().SequenceEqual(other.GetEqualityComponents());
    }

    public override bool Equals(object? obj) => Equals(obj as ValueObject);

    public override int GetHashCode() =>
        GetEqualityComponents().Aggregate(default(HashCode), (hash, component) =>
        {
            hash.Add(component);
            return hash;
        }).ToHashCode();

    public static bool operator ==(ValueObject? left, ValueObject? right) =>
        left is null ? right is null : left.Equals(right);

    public static bool operator !=(ValueObject? left, ValueObject? right) => !(left == right);
}
