using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface IPlayersService
{
    Task<PlayerStatusView> GetPlayerStatusAsync(Guid gameId, Guid playerId, CancellationToken ct = default);
    Task<IReadOnlyList<ConsolationHistoryEntry>> GetConsolationHistoryAsync(Guid playerId, CancellationToken ct = default);
    Task<PagedResult<PlayerStatusView>> GetGamePlayersAsync(Guid gameId, int page = 1, int pageSize = 20, CancellationToken ct = default);
}
