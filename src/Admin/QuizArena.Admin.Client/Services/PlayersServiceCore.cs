using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public class PlayersServiceCore(HttpClient http, string prefix) : IPlayersService
{
    private sealed record ApiParticipationStatus(
        Guid GameId,
        Guid PlayerId,
        string ParticipationStatus,
        int CurrentPoints,
        int SecuredPoints,
        DateTimeOffset? ExitedAt);

    private sealed record ApiConsolationItem(
        Guid GameId,
        string GameName,
        string Policy,
        int? Points,
        string? RewardName,
        DateTimeOffset Timestamp);

    private sealed record ApiConsolationHistory(Guid PlayerId, IReadOnlyList<ApiConsolationItem> Consolations);

    private sealed record ApiLeaderboardEntry(
        Guid PlayerId,
        string? DisplayName,
        int Rank,
        int Points,
        string Status,
        int SecuredPoints);

    private sealed record ApiLeaderboard(Guid GameId, IReadOnlyList<ApiLeaderboardEntry> Players);

    public async Task<PlayerStatusView> GetPlayerStatusAsync(Guid gameId, Guid playerId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiParticipationStatus>(
            $"{prefix}/games/{gameId}/players/{playerId}/status", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new PlayerStatusView(
            response.PlayerId, DisplayName: null, response.GameId, response.ParticipationStatus,
            response.CurrentPoints, response.SecuredPoints, response.ExitedAt);
    }

    public async Task<IReadOnlyList<ConsolationHistoryEntry>> GetConsolationHistoryAsync(Guid playerId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiConsolationHistory>(
            $"{prefix}/players/{playerId}/consolation-history", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return response.Consolations
            .Select(c => new ConsolationHistoryEntry(
                c.GameId, c.GameName, c.Policy, c.Points, c.RewardName, c.Timestamp))
            .ToList();
    }

    public async Task<PagedResult<PlayerStatusView>> GetGamePlayersAsync(Guid gameId, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        // The API exposes per-game players through the leaderboard aggregate.
        var leaderboard = await http.GetFromJsonAsync<ApiLeaderboard>($"{prefix}/games/{gameId}/leaderboard", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var players = leaderboard.Players
            .Select(p => new PlayerStatusView(
                p.PlayerId, p.DisplayName, gameId, p.Status, p.Points, p.SecuredPoints, ExitedAt: null))
            .ToList();
        var pageItems = players.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<PlayerStatusView>(pageItems, players.Count, page, pageSize);
    }
}
