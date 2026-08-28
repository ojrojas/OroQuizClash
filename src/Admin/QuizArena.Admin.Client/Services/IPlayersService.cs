using QuizArena.Admin.Client.Models;
using PlayersModels = QuizArena.Admin.Client.Models.Players;

namespace QuizArena.Admin.Client.Services;

public interface IPlayersService
{
    Task<PlayerStatusView> GetPlayerStatusAsync(Guid gameId, Guid playerId, CancellationToken ct = default);
    Task<IReadOnlyList<ConsolationHistoryEntry>> GetConsolationHistoryAsync(Guid playerId, CancellationToken ct = default);
    Task<PagedResult<PlayerStatusView>> GetGamePlayersAsync(Guid gameId, int page = 1, int pageSize = 20, CancellationToken ct = default);

    // 024 Admin Players — solo lectura
    Task<PagedResult<PlayersModels.PlayerSummary>> GetPlayersAsync(PlayersModels.PlayerFilter filter, CancellationToken ct = default);
    Task<PlayersModels.PlayerDetail> GetPlayerAsync(Guid playerId, CancellationToken ct = default);
    Task<PagedResult<PlayersModels.GameHistoryEntry>> GetPlayerGamesAsync(Guid playerId, PlayersModels.GameHistoryFilter filter, CancellationToken ct = default);
    Task<PagedResult<PlayersModels.PlayerParticipation>> GetParticipationsAsync(Guid playerId, PlayersModels.ParticipationFilter filter, CancellationToken ct = default);
    Task<PlayersModels.PlayerResult> GetResultAsync(Guid playerId, Guid gameId, CancellationToken ct = default);
    Task<PagedResult<PlayersModels.PointTransactionView>> GetScoresAsync(Guid playerId, PlayersModels.ScoreFilter filter, CancellationToken ct = default);
    Task<PagedResult<PlayersModels.PlayerRedemptionView>> GetRedemptionsAsync(Guid playerId, PlayersModels.RedemptionFilter filter, CancellationToken ct = default);
    Task<PlayersModels.PlayerStatistics> GetStatisticsAsync(Guid playerId, CancellationToken ct = default);
}
