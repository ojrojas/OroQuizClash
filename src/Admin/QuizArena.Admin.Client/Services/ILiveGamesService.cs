using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface ILiveGamesService
{
    Task<PagedResult<LiveGameSummary>> GetLiveGamesAsync(CancellationToken ct = default);
    Task<LiveGameSubscription> SubscribeAsync(Guid gameId, CancellationToken ct = default);
    Task StopGameAsync(Guid gameId, string reason, CancellationToken ct = default);
}
