using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Questions.Events;

public sealed record QuestionDeactivatedDomainEvent(Guid QuestionId) : DomainEvent;
