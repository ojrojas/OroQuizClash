using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Games;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class AllGamesWithPlayerSpecification : Specification<Game>
{
    public AllGamesWithPlayerSpecification(Guid playerId)
    {
        Where(g => g.Players.Any(p => p.UserId == playerId));
        AddInclude(g => g.Players);
        AddInclude(g => g.PointTransactions);
    }
}
