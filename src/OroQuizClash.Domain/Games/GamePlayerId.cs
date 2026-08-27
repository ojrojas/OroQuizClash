using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Games;

public sealed record GamePlayerId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static GamePlayerId New() => new(Guid.NewGuid());
}
