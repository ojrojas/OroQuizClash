using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Games;

public sealed record GameRoundId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static GameRoundId New() => new(Guid.NewGuid());
}
