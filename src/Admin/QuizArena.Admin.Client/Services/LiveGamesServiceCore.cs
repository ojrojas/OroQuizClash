using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// Shared REST snapshot + stop-game logic for Live Games. The SignalR subscription itself is
/// transport-specific (client: relative /hubs/game with cookie; server: resolved API origin
/// with the operator's access_token).
/// </summary>
public abstract class LiveGamesServiceCore(HttpClient http, string prefix) : ILiveGamesService
{
    private sealed record ApiGameResponse(
        Guid Id,
        string Name,
        string Status,
        Guid CategoryId,
        int MinRounds,
        int MaxRounds,
        int PlayerCount,
        int RoundCount,
        string RowVersion,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReadyAt,
        DateTimeOffset? StartedAt,
        DateTimeOffset? FinishedAt);

    private sealed record ApiPaginatedGames(IReadOnlyList<ApiGameResponse> Items, int Total, int Page, int PageSize);

    private sealed record ApiReasonRequest(Guid GameId, string Reason);

    public async Task<PagedResult<LiveGameSummary>> GetLiveGamesAsync(CancellationToken ct = default)
    {
        var result = await http.GetFromJsonAsync<ApiPaginatedGames>(
            $"{prefix}/games?status=IN_PROGRESS&page=1&pageSize=50", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = result.Items.Select(g => new LiveGameSummary(
            g.Id, g.Name, g.CategoryId, g.PlayerCount, g.RoundCount,
            GameStatusMap.FromApi(g.Status), g.StartedAt)).ToList();
        return new PagedResult<LiveGameSummary>(items, result.Total, result.Page, result.PageSize);
    }

    public abstract Task<LiveGameSubscription> SubscribeAsync(Guid gameId, CancellationToken ct = default);

    public async Task StopGameAsync(Guid gameId, string reason, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"{prefix}/games/{gameId}/force-finish", new ApiReasonRequest(gameId, reason), ct);
        await response.ThrowIfApiErrorAsync(ct);
    }
}
