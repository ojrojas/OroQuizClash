using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Models.LiveGame;

namespace QuizArena.Admin.Client.Services;

public interface ILiveGameService
{
    Task<PagedResult<LiveGameView>> GetLiveGamesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<LiveGameView> GetLiveGameAsync(Guid gameId, CancellationToken ct = default);
    Task<IReadOnlyList<LiveScore>> GetLeaderboardAsync(Guid gameId, CancellationToken ct = default);
}
