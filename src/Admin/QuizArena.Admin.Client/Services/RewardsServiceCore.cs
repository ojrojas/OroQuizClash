using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;
using RewardsModels = QuizArena.Admin.Client.Models.Rewards;

namespace QuizArena.Admin.Client.Services;

public class RewardsServiceCore(HttpClient http, string prefix) : IRewardsService
{
    private sealed record ApiRewardItem(
        Guid Id,
        string Name,
        string Description,
        int PointsRequired,
        int Stock,
        string Status,
        DateTimeOffset? ExpirationDate,
        bool Available);

    private sealed record ApiRewardsResponse(IReadOnlyList<ApiRewardItem> Rewards, int? AvailablePoints, Guid? GameId);

    private sealed record ApiRewardRequest(
        string Name,
        string Description,
        int PointsRequired,
        int Stock,
        DateTimeOffset? ExpirationDate);

    private sealed record ApiV2RewardItem(
        Guid Id,
        string Name,
        string? Description,
        string Type,
        int Cost,
        int Stock,
        DateTimeOffset? AvailableFrom,
        DateTimeOffset? AvailableTo,
        string Status,
        bool IsEligible,
        string RowVersion,
        DateTimeOffset? CreatedAt,
        IReadOnlyList<ApiRewardHistoryItem>? History);

    private sealed record ApiRewardHistoryItem(string From, string To, DateTimeOffset Timestamp, string ActorId, string? Reason);

    private sealed record ApiV2RewardsPagedResponse(IReadOnlyList<ApiV2RewardItem> Items, int TotalCount, int Page, int PageSize, int TotalPages);

    private sealed record ApiV2CreateRequest(
        string Name,
        string? Description,
        string Type,
        int Cost,
        int Stock,
        DateTimeOffset? AvailableFrom,
        DateTimeOffset? AvailableTo);

    private sealed record ApiV2UpdateRequest(
        string Name,
        string? Description,
        string Type,
        int Cost,
        int Stock,
        DateTimeOffset? AvailableFrom,
        DateTimeOffset? AvailableTo,
        string RowVersion);

    private sealed record ApiRowVersionRequest(string RowVersion, string IdempotencyKey);

    public async Task<PagedResult<Models.RewardSummary>> GetRewardsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        // GET /api/rewards returns the full catalog (player-oriented); paginate client-side.
        var response = await http.GetFromJsonAsync<ApiRewardsResponse>($"{prefix}/rewards?includeUnavailable=true", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = response.Rewards.Select(Map).ToList();
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<Models.RewardSummary>(pageItems, items.Count, page, pageSize);
    }

    public async Task<Models.RewardSummary> CreateRewardAsync(Models.RewardForm form, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"{prefix}/rewards", ToRequest(form), ct);
        return Map(await response.ReadAsAsync<ApiRewardItem>(ct));
    }

    public async Task<Models.RewardSummary> UpdateRewardAsync(Guid rewardId, Models.RewardForm form, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"{prefix}/rewards/{rewardId}", ToRequest(form), ct);
        return Map(await response.ReadAsAsync<ApiRewardItem>(ct));
    }

    public Task ActivateRewardAsync(Guid rewardId, CancellationToken ct = default) =>
        PostAsync($"{prefix}/rewards/{rewardId}/activate", ct);

    public Task DeactivateRewardAsync(Guid rewardId, CancellationToken ct = default) =>
        PostAsync($"{prefix}/rewards/{rewardId}/deactivate", ct);

    public async Task<PagedResult<RewardsModels.RewardSummary>> GetRewardsV2Async(RewardsModels.RewardFilter filter, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["type"] = filter.Type is null ? null : RewardsModels.RewardTypeMap.ToApi(filter.Type.Value),
            ["status"] = filter.Status is null ? null : RewardsModels.RewardStateMap.ToApi(filter.Status.Value),
            ["search"] = filter.Search,
            ["onlyEligible"] = filter.OnlyEligible?.ToString().ToLowerInvariant(),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        try
        {
            var response = await http.GetFromJsonAsync<ApiV2RewardsPagedResponse>($"{prefix}/rewards{query}", ct);
            if (response is not null)
            {
                var items = response.Items.Select(MapV2Summary).ToList();
                return new PagedResult<Models.Rewards.RewardSummary>(items, response.TotalCount, response.Page, response.PageSize);
            }
        }
        catch { }
        // Fallback: try legacy full catalog and filter client-side
        var legacy = await http.GetFromJsonAsync<ApiRewardsResponse>($"{prefix}/rewards?includeUnavailable=true", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var all = legacy.Rewards.Select(MapV2FromLegacy).ToList();
        if (filter.Type is not null) all = all.Where(r => r.Type == filter.Type.Value).ToList();
        if (filter.Status is not null) all = all.Where(r => r.Status == filter.Status.Value).ToList();
        if (!string.IsNullOrWhiteSpace(filter.Search)) all = all.Where(r => r.Name.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)).ToList();
        if (filter.OnlyEligible == true) all = all.Where(r => r.IsEligible).ToList();
        var pageItems = all.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return new PagedResult<Models.Rewards.RewardSummary>(pageItems, all.Count, filter.Page, filter.PageSize);
    }

    public async Task<RewardsModels.RewardDetail> GetRewardAsync(Guid id, CancellationToken ct = default)
    {
        var item = await http.GetFromJsonAsync<ApiV2RewardItem>($"{prefix}/rewards/{id}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return MapV2Detail(item);
    }

    public async Task<RewardsModels.RewardDetail> CreateRewardAsync(RewardsModels.RewardForm form, CancellationToken ct = default)
    {
        var request = new ApiV2CreateRequest(form.Name.Trim(), form.Description, RewardsModels.RewardTypeMap.ToApi(form.Type), form.Cost, form.Stock, form.AvailableFrom, form.AvailableTo);
        var response = await http.PostAsJsonAsync($"{prefix}/rewards", request, ct);
        await response.ThrowIfApiErrorAsync(ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        // Intenta V2 primero; si el Api aún responde con shape legado (CreateRewardResponse) se hace fallback
        // para evitar la excepción que ve el usuario en la pantalla de creación.
        try
        {
            var v2 = System.Text.Json.JsonSerializer.Deserialize<ApiV2RewardItem>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (v2 is not null && !string.IsNullOrEmpty(v2.RowVersion))
                return MapV2Detail(v2);
        }
        catch { }
        try
        {
            var legacy = System.Text.Json.JsonSerializer.Deserialize<ApiRewardItem>(json, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
            if (legacy is not null)
                return new RewardsModels.RewardDetail(legacy.Id, legacy.Name, legacy.Description, form.Type, form.Cost, legacy.Stock, form.AvailableFrom, legacy.ExpirationDate ?? form.AvailableTo, RewardsModels.RewardStateMap.FromApi(legacy.Status), true, "legacy", []);
        }
        catch { }
        // Último recurso: buscar en catálogo por nombre
        var legacyList = await http.GetFromJsonAsync<ApiRewardsResponse>($"{prefix}/rewards?includeUnavailable=true", ct);
        var match = legacyList?.Rewards.FirstOrDefault(r => r.Name.Equals(form.Name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (match is not null)
            return new RewardsModels.RewardDetail(match.Id, match.Name, match.Description, form.Type, form.Cost, match.Stock, form.AvailableFrom, form.AvailableTo ?? match.ExpirationDate, RewardsModels.RewardStateMap.FromApi(match.Status), true, "legacy", []);
        throw new ApiErrorException(ApiErrorView.Unknown);
    }

    public async Task<RewardsModels.RewardDetail> UpdateRewardAsync(Guid id, RewardsModels.UpdateRewardRequest request, CancellationToken ct = default)
    {
        var payload = new ApiV2UpdateRequest(request.Name.Trim(), request.Description, RewardsModels.RewardTypeMap.ToApi(request.Type), request.Cost, request.Stock, request.AvailableFrom, request.AvailableTo, request.RowVersion);
        using var message = new HttpRequestMessage(HttpMethod.Put, $"{prefix}/rewards/{id}")
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.TryAddWithoutValidation("If-Match", $"W/\"{request.RowVersion}\"");
        message.Headers.TryAddWithoutValidation("X-Idempotency-Key", Guid.NewGuid().ToString());
        using var response = await http.SendAsync(message, ct);
        var item = await response.ReadAsAsync<ApiV2RewardItem>(ct);
        return MapV2Detail(item);
    }

    public async Task<RewardsModels.RewardDetail> ActivateAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default)
    {
        return await PostWithRowVersionAsync($"{prefix}/rewards/{id}/activate", rowVersion, idempotencyKey, ct);
    }

    public async Task<RewardsModels.RewardDetail> DeactivateAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default)
    {
        return await PostWithRowVersionAsync($"{prefix}/rewards/{id}/deactivate", rowVersion, idempotencyKey, ct);
    }

    public async Task<RewardsModels.RewardDetail> ArchiveAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default)
    {
        return await PostWithRowVersionAsync($"{prefix}/rewards/{id}/archive", rowVersion, idempotencyKey, ct);
    }

    private async Task<RewardsModels.RewardDetail> PostWithRowVersionAsync(string url, string rowVersion, string idempotencyKey, CancellationToken ct)
    {
        var payload = new ApiRowVersionRequest(rowVersion, idempotencyKey);
        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.TryAddWithoutValidation("If-Match", $"W/\"{rowVersion}\"");
        message.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        using var response = await http.SendAsync(message, ct);
        var item = await response.ReadAsAsync<ApiV2RewardItem>(ct);
        return MapV2Detail(item);
    }

    private async Task PostAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { }) };
        using var response = await http.SendAsync(request, ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    private static ApiRewardRequest ToRequest(Models.RewardForm form) =>
        new(form.Name.Trim(), form.Description, form.PointCost, form.Stock ?? 0, form.ExpirationDate);

    private static Models.RewardSummary Map(ApiRewardItem r) => new(
        r.Id, r.Name, r.Description, r.PointsRequired, r.Stock,
        Models.RewardStatusMap.FromApi(r.Status), r.ExpirationDate, r.Available);

    private static RewardsModels.RewardSummary MapV2Summary(ApiV2RewardItem r) => new(
        r.Id, r.Name, RewardsModels.RewardTypeMap.FromApi(r.Type), r.Cost, r.Stock,
        RewardsModels.RewardStateMap.FromApi(r.Status), r.IsEligible, r.RowVersion);

    private static RewardsModels.RewardDetail MapV2Detail(ApiV2RewardItem r) => new(
        r.Id, r.Name, r.Description, RewardsModels.RewardTypeMap.FromApi(r.Type), r.Cost, r.Stock,
        r.AvailableFrom, r.AvailableTo, RewardsModels.RewardStateMap.FromApi(r.Status), r.IsEligible, r.RowVersion,
        r.History?.Select(h => new RewardsModels.RewardStateTransition(RewardsModels.RewardStateMap.FromApi(h.From), RewardsModels.RewardStateMap.FromApi(h.To), h.Timestamp, h.ActorId, h.Reason)).ToList() ?? []);

    private static RewardsModels.RewardSummary MapV2FromLegacy(ApiRewardItem r) => new(
        r.Id, r.Name, RewardsModels.RewardType.Physical, r.PointsRequired, r.Stock,
        r.Status.Equals("ACTIVE", StringComparison.OrdinalIgnoreCase) ? RewardsModels.RewardStateView.Active : RewardsModels.RewardStateView.Inactive,
        r.Available, "legacy");
}
