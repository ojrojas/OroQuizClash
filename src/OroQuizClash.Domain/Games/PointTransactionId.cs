using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Games;

public sealed record PointTransactionId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static PointTransactionId New() => new(Guid.NewGuid());
}
