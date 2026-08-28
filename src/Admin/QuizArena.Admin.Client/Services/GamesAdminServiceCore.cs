using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

/// <summary>
/// Shared implementation of <see cref="IGamesAdminService"/> used by both transports:
/// Client (WASM → /bff/games*, cookie) and Server (InteractiveServer → /api/games*, Bearer).
/// The route prefix is the only difference (contracts/service-interfaces.md dual contract).
/// </summary>
public class GamesAdminServiceCore(HttpClient http, string prefix) : IGamesAdminService
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

    private sealed record ApiLeaderboardEntry(
        Guid PlayerId,
        string? DisplayName,
        int Rank,
        int Points,
        int CorrectAnswers,
        int? CurrentLevel,
        string Status,
        int SecuredPoints);

    private sealed record ApiLeaderboard(Guid GameId, IReadOnlyList<ApiLeaderboardEntry> Players);

    private sealed record ApiCreateGameRequest(
        string Name,
        Guid CategoryId,
        int MinRounds,
        int MaxRounds,
        int InitialDifficulty,
        string DifficultyStrategy,
        int TimeLimitPerQuestionSeconds,
        string ScoringSystem,
        string LossPolicy,
        string WithdrawalPolicy,
        string ConsolationPolicy,
        string RewardType,
        int RewardThreshold,
        int MinPlayers,
        int MaxPlayers);

    private sealed record ApiCreateGameResponse(Guid GameId, string Status);

    private sealed record ApiReasonRequest(Guid GameId, string Reason);

    public async Task<PagedResult<GameSummary>> GetGamesAsync(GameFilter filter, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["status"] = GameStatusMap.ToApiQuery(filter.Status),
            ["categoryId"] = filter.CategoryId?.ToString(),
            ["search"] = filter.Search,
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var result = await http.GetFromJsonAsync<ApiPaginatedGames>($"{prefix}/games{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new PagedResult<GameSummary>(
            result.Items.Select(MapSummary).ToList(),
            result.Total, result.Page, result.PageSize);
    }

    public async Task<GameDetail> GetGameAsync(Guid gameId, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiGameResponse>($"{prefix}/games/{gameId}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var leaderboard = await GetLeaderboardAsync(gameId, ct).ConfigureAwait(false);
        return new GameDetail(
            response.Id, response.Name, response.CategoryId, GameStatusMap.FromApi(response.Status),
            response.MinRounds, response.MaxRounds, response.PlayerCount, response.RoundCount,
            response.RowVersion, response.CreatedAt, response.ReadyAt, response.StartedAt,
            response.FinishedAt, leaderboard);
    }

    public async Task<GameDetail> CreateGameAsync(GameConfigurationForm form, CancellationToken ct = default)
    {
        var postResponse = await http.PostAsJsonAsync($"{prefix}/games", ToApiRequest(form), ct);
        var response = await postResponse.ReadAsAsync<ApiCreateGameResponse>(ct);
        return await GetGameAsync(response.GameId, ct);
    }

    public async Task<GameDetail> UpdateGameAsync(Guid gameId, GameConfigurationForm form, CancellationToken ct = default)
    {
        // The current API has no PUT /api/games/{id}; configuration is immutable after creation
        // (Constitution C). Re-creation is not implied — surface as unsupported until the API adds it.
        var response = await http.PutAsJsonAsync($"{prefix}/games/{gameId}", ToApiRequest(form), ct);
        await response.ThrowIfApiErrorAsync(ct);
        return await GetGameAsync(gameId, ct);
    }

    public Task StartGameAsync(Guid gameId, CancellationToken ct = default) =>
        PostEmptyAsync($"{prefix}/games/{gameId}/start", ct);

    public async Task CancelGameAsync(Guid gameId, string reason, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"{prefix}/games/{gameId}/cancel", new ApiReasonRequest(gameId, reason), ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    public Task FinishGameAsync(Guid gameId, CancellationToken ct = default) =>
        PostEmptyAsync($"{prefix}/games/{gameId}/finish", ct);

    public async Task ForceFinishGameAsync(Guid gameId, string reason, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"{prefix}/games/{gameId}/force-finish", new ApiReasonRequest(gameId, reason), ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    public Task OpenLobbyAsync(Guid gameId, CancellationToken ct = default) =>
        PostEmptyAsync($"{prefix}/games/{gameId}/open-lobby", ct);

    public Task MarkReadyAsync(Guid gameId, CancellationToken ct = default) =>
        PostEmptyAsync($"{prefix}/games/{gameId}/ready", ct);

    public async Task<IReadOnlyList<LeaderboardEntry>> GetLeaderboardAsync(Guid gameId, CancellationToken ct = default)
    {
        var leaderboard = await http.GetFromJsonAsync<ApiLeaderboard>($"{prefix}/games/{gameId}/leaderboard", ct);
        if (leaderboard?.Players is null)
        {
            return [];
        }
        return leaderboard.Players
            .OrderBy(p => p.Rank)
            .Select(p => new LeaderboardEntry(
                p.PlayerId,
                string.IsNullOrWhiteSpace(p.DisplayName) ? ShortId(p.PlayerId) : p.DisplayName!,
                p.Rank, p.Points, p.SecuredPoints, p.Status, IsCurrentOperator: false))
            .ToList();
    }

    private static string ShortId(Guid id) => $"Player {id.ToString()[..8]}";

    private async Task PostEmptyAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(new { })
        };
        using var response = await http.SendAsync(request, ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    private static ApiCreateGameRequest ToApiRequest(GameConfigurationForm form) => new(
        form.Name.Trim(),
        form.CategoryId,
        form.Rounds,
        form.Rounds,
        form.Difficulty,
        form.DifficultyStrategy,
        form.TimeLimitSeconds,
        form.ScoringSystem,
        form.LossPolicy,
        form.WithdrawalPolicy,
        form.ConsolationPolicy,
        form.RewardType,
        form.RewardThreshold,
        form.MinPlayers,
        form.MaxPlayers);

    private static GameSummary MapSummary(ApiGameResponse g) => new(
        g.Id, g.Name, g.CategoryId, GameStatusMap.FromApi(g.Status),
        g.MinRounds, g.MaxRounds, g.PlayerCount, g.RoundCount,
        g.CreatedAt, g.ReadyAt, g.StartedAt, g.FinishedAt);
}

internal static class QueryString
{
    public static string Build(Dictionary<string, string?> parameters)
    {
        var pairs = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value!)}");
        var query = string.Join('&', pairs);
        return query.Length == 0 ? string.Empty : $"?{query}";
    }
}
