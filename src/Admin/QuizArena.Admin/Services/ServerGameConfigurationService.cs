using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Services;
using GC = QuizArena.Admin.Client.Models.GameConfiguration;

namespace QuizArena.Admin.Services;

public sealed class ServerGameConfigurationService(HttpClient http) : QuizArena.Admin.Client.Services.IGameConfigurationService
{
    private const string Prefix = "/api";

    public async Task<GC.GameDetail> CreateAsync(GC.CreateGameRequest request, CancellationToken ct = default)
    {
        var apiPayload = new
        {
            name = request.Name,
            categoryId = request.CategoryId,
            minRounds = request.NumberOfRounds,
            maxRounds = request.NumberOfRounds,
            initialDifficulty = request.InitialDifficulty,
            difficultyStrategy = GC.PolicyCatalogs.ToApi(request.DifficultyProgression),
            timeLimitPerQuestionSeconds = request.TimePerQuestion,
            scoringSystem = GC.PolicyCatalogs.ToApi(request.Scoring),
            lossPolicy = GC.PolicyCatalogs.ToApi(request.FinishPolicy),
            withdrawalPolicy = GC.PolicyCatalogs.ToApi(request.WithdrawalPolicy),
            consolationPolicy = "None",
            rewardType = "Points",
            rewardThreshold = request.PointsPerRound,
            minPlayers = 2,
            maxPlayers = request.MaxPlayers
        };
        var postResponse = await http.PostAsJsonAsync($"{Prefix}/games", apiPayload, ct);
        await postResponse.ThrowIfApiErrorAsync(ct);
        var created = await postResponse.ReadAsAsync<CreateGameResponse>(ct);
        return await GetAsync(created.GameId, ct);
    }

    private sealed record CreateGameResponse(Guid GameId, string Status);

    public async Task<GC.GameDetail> UpdateAsync(Guid id, GC.UpdateGameRequest request, CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, $"{Prefix}/games/{id}")
        {
            Content = JsonContent.Create(request)
        };
        req.Headers.TryAddWithoutValidation("If-Match", $"W/\"{request.RowVersion}\"");
        var response = await http.SendAsync(req, ct);
        var result = await response.ReadAsAsync<GC.GameDetail>(ct);
        return result;
    }

    public async Task<PagedResult<GC.GameSummary>> ListAsync(GC.GameFilter filter, CancellationToken ct = default)
    {
        var query = BuildQuery(new Dictionary<string, string?>
        {
            ["status"] = filter.Status is not null ? GC.GameStateViewMap.ToApi(filter.Status.Value) : null,
            ["categoryId"] = filter.CategoryId?.ToString(),
            ["search"] = filter.Search,
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var result = await http.GetFromJsonAsync<PagedResult<GC.GameSummary>>($"{Prefix}/games{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return result;
    }

    public async Task<GC.GameDetail> GetAsync(Guid id, CancellationToken ct = default)
    {
        var api = await http.GetFromJsonAsync<ApiGameResponse>($"{Prefix}/games/{id}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new GC.GameDetail(
            api.Id, api.Name, null, api.CategoryId, "Unknown",
            GC.GameStateViewMap.FromApi(api.Status),
            api.MaxRounds, 10, 30, 3,
            GC.DifficultyStrategy.Linear, GC.ScoringSystem.Standard, 100,
            GC.SecuredPointsPolicy.None, GC.WithdrawalPolicy.LoseAll, GC.LossPolicy.LoseAll,
            null, null, null,
            api.RowVersion, api.CreatedAt, new List<GC.GameStateTransition>()
        );
    }

    private sealed record ApiGameResponse(Guid Id, string Name, string Status, Guid CategoryId, int MinRounds, int MaxRounds, int PlayerCount, int RoundCount, string RowVersion, DateTimeOffset CreatedAt, DateTimeOffset? ReadyAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt);

    public Task<GC.GameDetail> ScheduleAsync(Guid id, DateTimeOffset scheduledAt, string rowVersion, CancellationToken ct = default) =>
        TransitionAsync(id, "schedule", new { scheduledAt, rowVersion }, ct);

    public Task<GC.GameDetail> ReadyAsync(Guid id, string rowVersion, CancellationToken ct = default) =>
        TransitionAsync(id, "ready", new { rowVersion }, ct);

    public Task<GC.GameDetail> StartAsync(Guid id, string rowVersion, CancellationToken ct = default) =>
        TransitionAsync(id, "start", new { rowVersion }, ct);

    public Task<GC.GameDetail> PauseAsync(Guid id, string rowVersion, CancellationToken ct = default) =>
        TransitionAsync(id, "pause", new { rowVersion }, ct);

    public Task<GC.GameDetail> ResumeAsync(Guid id, string rowVersion, CancellationToken ct = default) =>
        TransitionAsync(id, "resume", new { rowVersion }, ct);

    public Task<GC.GameDetail> FinishAsync(Guid id, string rowVersion, CancellationToken ct = default) =>
        TransitionAsync(id, "finish", new { rowVersion }, ct);

    public Task<GC.GameDetail> CancelAsync(Guid id, string rowVersion, CancellationToken ct = default) =>
        TransitionAsync(id, "cancel", new { rowVersion }, ct);

    private async Task<GC.GameDetail> TransitionAsync(Guid id, string action, object payload, CancellationToken ct)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"{Prefix}/games/{id}/{action}")
        {
            Content = JsonContent.Create(payload)
        };
        if (payload is not null)
        {
            var prop = payload.GetType().GetProperty("rowVersion");
            if (prop?.GetValue(payload) is string rv)
                req.Headers.TryAddWithoutValidation("If-Match", $"W/\"{rv}\"");
        }
        var response = await http.SendAsync(req, ct);
        await response.ThrowIfApiErrorAsync(ct);
        var api = await response.ReadAsAsync<ApiGameResponse>(ct);
        return new GC.GameDetail(
            api.Id, api.Name, null, api.CategoryId, "Unknown",
            GC.GameStateViewMap.FromApi(api.Status),
            api.MaxRounds, 10, 30, 3,
            GC.DifficultyStrategy.Linear, GC.ScoringSystem.Standard, 100,
            GC.SecuredPointsPolicy.None, GC.WithdrawalPolicy.LoseAll, GC.LossPolicy.LoseAll,
            null, null, null,
            api.RowVersion, api.CreatedAt, new List<GC.GameStateTransition>()
        );
    }

    private static string BuildQuery(Dictionary<string, string?> p)
    {
        var q = string.Join("&", p.Where(kv => kv.Value is not null).Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value!)}"));
        return q.Length == 0 ? string.Empty : "?" + q;
    }
}
