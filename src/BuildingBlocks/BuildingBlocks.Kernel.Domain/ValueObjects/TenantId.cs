namespace BuildingBlocks.Kernel.Domain.ValueObjects;

/// <summary>
/// Strongly typed value object that identifies a tenant across the whole
/// application. Aggregates, entities and tenant-scoped queries should model
/// tenant identity with this type instead of a raw <see cref="Guid"/> so the
/// ubiquitous language ("tenant") is expressed in the domain model.
/// Persists as a <c>uuid</c> column via a value converter.
/// </summary>
public sealed record TenantId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static TenantId New() => new(Guid.NewGuid());

    public static TenantId From(Guid value) => new(value);

    /// <summary>Allows callers that hold a raw <see cref="Guid"/> (e.g. API
    /// boundaries, test fixtures) to build the value object without ceremony.</summary>
    public static implicit operator TenantId(Guid value) => new(value);
}