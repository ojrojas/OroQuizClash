using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Rewards.Events;

public sealed record RewardStatusChangedDomainEvent(Guid RewardId, string StatusName) : DomainEvent;
