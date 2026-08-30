using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface IGamesAdminService
{
    Task<PagedResult<GameSummary>> GetGamesAsync(GameFilter filter, CancellationToken ct = default);
    Task<GameDetail> GetGameAsync(Guid gameId, CancellationToken ct = default);
    Task<GameDetail> CreateGameAsync(GameConfigurationForm form, CancellationToken ct = default);
    Task<GameDetail> UpdateGameAsync(Guid gameId, GameConfigurationForm form, CancellationToken ct = default);
    Task StartGameAsync(Guid gameId, CancellationToken ct = default);
    Task CancelGameAsync(Guid gameId, string reason, CancellationToken ct = default);
    Task FinishGameAsync(Guid gameId, CancellationToken ct = default);
    Task ForceFinishGameAsync(Guid gameId, string reason, CancellationToken ct = default);
    Task OpenLobbyAsync(Guid gameId, CancellationToken ct = default);
    Task MarkReadyAsync(Guid gameId, CancellationToken ct = default);
    Task StartRoundAsync(Guid gameId, CancellationToken ct = default);
    Task CompleteRoundAsync(Guid gameId, Guid roundId, CancellationToken ct = default);
    Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(Guid gameId, CancellationToken ct = default);
}
