using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Questions.Events;

public sealed record QuestionCreatedDomainEvent(Guid QuestionId, Guid CategoryId) : DomainEvent;
