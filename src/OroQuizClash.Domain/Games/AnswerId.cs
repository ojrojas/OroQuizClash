using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Games;

public sealed record AnswerId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static AnswerId New() => new(Guid.NewGuid());
}
