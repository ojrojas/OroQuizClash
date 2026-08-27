using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Questions.Events;

public sealed record QuestionUpdatedDomainEvent(Guid QuestionId) : DomainEvent;
