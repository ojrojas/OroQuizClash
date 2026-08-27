using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Questions.Events;

public sealed record QuestionPublishedDomainEvent(Guid QuestionId, Guid CategoryId) : DomainEvent;
