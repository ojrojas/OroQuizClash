using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Rewards.Events;

public sealed record RewardUpdatedDomainEvent(Guid RewardId) : DomainEvent;
