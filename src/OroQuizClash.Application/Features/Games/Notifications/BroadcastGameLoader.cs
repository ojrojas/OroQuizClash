using BuildingBlocks.Kernel.Domain.Repositories;

using OroQuizClash.Domain.Games;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games.Notifications;

internal static class BroadcastGameLoader
{
    public static Task<Game?> LoadGameAsync(
        IRepository<Game, GameId> repository, Guid gameId, CancellationToken cancellationToken) =>
        repository.FirstOrDefaultAsync(new GameByIdWithAnswersSpecification(new GameId(gameId)), cancellationToken);
}
