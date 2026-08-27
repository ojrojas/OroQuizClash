using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Rewards.Events;

public sealed record RedemptionStatusChangedDomainEvent(
    Guid RedemptionId,
    string StatusName,
    Guid ActorId) : DomainEvent;
