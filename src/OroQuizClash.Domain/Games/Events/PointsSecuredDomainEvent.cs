using BuildingBlocks.Kernel.Domain.Events;

namespace OroQuizClash.Domain.Games.Events;

public sealed record PointsSecuredDomainEvent(
    Guid GameId,
    Guid PlayerId,
    int SecuredAmount,
    int TotalSecured) : DomainEvent;
