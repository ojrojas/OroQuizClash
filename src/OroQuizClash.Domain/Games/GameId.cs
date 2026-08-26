using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Games;

public sealed record GameId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static GameId New() => new(Guid.NewGuid());
}