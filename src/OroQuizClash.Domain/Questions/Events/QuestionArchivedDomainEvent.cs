using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Questions.Events;

public sealed record QuestionArchivedDomainEvent(Guid QuestionId) : DomainEvent;
