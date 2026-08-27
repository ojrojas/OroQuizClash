using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Rewards;

public sealed record RewardId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static RewardId New() => new(Guid.NewGuid());
}
