namespace BuildingBlocks.Kernel.Domain.ValueObjects;

/// <summary>
/// Base record for strongly typed identifiers, avoiding primitive obsession:
/// <code>public sealed record OrderId(Guid Value) : StronglyTypedId&lt;Guid&gt;(Value);</code>
/// </summary>
public abstract record StronglyTypedId<TValue>(TValue Value)
    where TValue : notnull
{
    public sealed override string ToString() => Value.ToString() ?? string.Empty;
}
