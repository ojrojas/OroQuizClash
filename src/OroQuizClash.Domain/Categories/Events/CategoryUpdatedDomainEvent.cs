using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Categories.Events;

public sealed record CategoryUpdatedDomainEvent(Guid CategoryId) : DomainEvent;