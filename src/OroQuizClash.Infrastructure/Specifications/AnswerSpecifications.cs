using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Games;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class GameByIdWithAnswersSpecification : Specification<Game>
{
    public GameByIdWithAnswersSpecification(GameId id)
    {
        Where(g => g.Id == id);
        AddInclude(g => g.Players);
        AddInclude(g => g.Rounds);
        AddInclude(g => g.Answers);
        AddInclude(g => g.PointTransactions);
    }
}

public sealed class AnswerByIdSpecification : Specification<Answer>
{
    public AnswerByIdSpecification(GameId gameId, AnswerId answerId)
    {
        Where(a => a.GameId == gameId && a.Id == answerId);
        ApplyAsNoTracking();
    }
}

public sealed class AnswersByGameAndPlayerSpecification : Specification<Answer>
{
    public AnswersByGameAndPlayerSpecification(GameId gameId, Guid playerId)
    {
        Where(a => a.GameId == gameId && a.PlayerId == playerId);
        ApplyAsNoTracking();
        ApplyOrderBy(a => a.RoundId);
    }
}

public sealed class PointTransactionsByGameSpecification : Specification<PointTransaction>
{
    public PointTransactionsByGameSpecification(GameId gameId)
    {
        Where(pt => pt.GameId == gameId);
        ApplyAsNoTracking();
    }
}
