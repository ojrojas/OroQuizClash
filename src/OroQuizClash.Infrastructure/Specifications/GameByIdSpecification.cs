using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Games;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class GameByIdSpecification : Specification<Game>
{
    public GameByIdSpecification(GameId id)
    {
        Where(g => g.Id == id);
    }
}