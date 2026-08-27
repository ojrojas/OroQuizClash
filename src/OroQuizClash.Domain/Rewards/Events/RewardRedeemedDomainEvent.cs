using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Rewards.Events;

public sealed record RewardRedeemedDomainEvent(
    Guid RedemptionId,
    Guid RewardId,
    Guid PlayerId,
    Guid GameId,
    int Points) : DomainEvent;
