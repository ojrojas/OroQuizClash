using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Rewards;

public sealed record RewardRedemptionId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static RewardRedemptionId New() => new(Guid.NewGuid());
}
