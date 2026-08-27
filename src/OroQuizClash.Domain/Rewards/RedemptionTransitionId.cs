using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Rewards;

public sealed record RedemptionTransitionId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static RedemptionTransitionId New() => new(Guid.NewGuid());
}
