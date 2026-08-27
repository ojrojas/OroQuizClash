using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Rewards.Events;

public sealed record RewardCreatedDomainEvent(Guid RewardId) : DomainEvent;
