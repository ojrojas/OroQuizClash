using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;
using RewardsModels = QuizArena.Admin.Client.Models.Rewards;

namespace QuizArena.Admin.Client.Services;

public class RedemptionsServiceCore(HttpClient http, string prefix) : IRedemptionsService
{
    private sealed record ApiRedemptionTransition(string Status, Guid ActorId, DateTimeOffset At);

    private sealed record ApiRedemption(
        Guid Id,
        Guid PlayerId,
        Guid RewardId,
        Guid GameId,
        int Points,
        string Status,
        DateTimeOffset RequestedAt,
        DateTimeOffset? DeliveredAt,
        IReadOnlyList<ApiRedemptionTransition> Transitions);

    private sealed record ApiRedemptionsResponse(IReadOnlyList<ApiRedemption> Redemptions);

    private sealed record ApiV2Redemption(
        Guid RedemptionId,
        Guid RewardId,
        string RewardName,
        string RewardType,
        Guid PlayerId,
        string PlayerName,
        int Cost,
        string Status,
        DateTimeOffset RequestedAt,
        DateTimeOffset? ApprovedAt,
        DateTimeOffset? RejectedAt,
        DateTimeOffset? DeliveredAt,
        string? Reason,
        bool IsConsolation,
        string RowVersion);

    private sealed record ApiV2RedemptionsPagedResponse(IReadOnlyList<ApiV2Redemption> Items, int TotalCount, int Page, int PageSize);

    private sealed record ApiRowVersionRequest(string RowVersion, string IdempotencyKey);
    private sealed record ApiRejectRequest(string RowVersion, string IdempotencyKey, string Reason);
    private sealed record ApiCancelRequest(string RowVersion, string IdempotencyKey, string? Reason);

    public async Task<PagedResult<Models.RedemptionSummary>> GetRedemptionsAsync(Models.RedemptionFilter filter, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["status"] = filter.Status is null ? null : Models.RedemptionStatusMap.ToApi(filter.Status.Value)
        });
        var response = await http.GetFromJsonAsync<ApiRedemptionsResponse>($"{prefix}/redemptions/all{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = response.Redemptions
            .OrderByDescending(r => r.RequestedAt)
            .Select(Map)
            .ToList();
        var pageItems = items.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return new PagedResult<RedemptionSummary>(pageItems, items.Count, filter.Page, filter.PageSize);
    }

    public Task ApproveAsync(Guid redemptionId, CancellationToken ct = default) =>
        PostAsync($"{prefix}/redemptions/{redemptionId}/approve", ct);

    public Task RejectAsync(Guid redemptionId, CancellationToken ct = default) =>
        PostAsync($"{prefix}/redemptions/{redemptionId}/reject", ct);

    public Task CancelAsync(Guid redemptionId, CancellationToken ct = default) =>
        PostAsync($"{prefix}/redemptions/{redemptionId}/cancel", ct);

    public Task DeliverAsync(Guid redemptionId, CancellationToken ct = default) =>
        PostAsync($"{prefix}/redemptions/{redemptionId}/deliver", ct);

    public async Task<PagedResult<RewardsModels.RewardRedemption>> GetRedemptionsV2Async(RewardsModels.RedemptionFilter filter, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["status"] = filter.Status is null ? null : RewardsModels.RedemptionStateMap.ToApi(filter.Status.Value),
            ["type"] = filter.Type is null ? null : RewardsModels.RewardTypeMap.ToApi(filter.Type.Value),
            ["playerId"] = filter.PlayerId?.ToString(),
            ["search"] = filter.Search,
            ["from"] = filter.From?.ToString("O"),
            ["to"] = filter.To?.ToString("O"),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        try
        {
            var response = await http.GetFromJsonAsync<ApiV2RedemptionsPagedResponse>($"{prefix}/redemptions{query}", ct);
            if (response is not null)
            {
                var items = response.Items.Select(MapV2).ToList();
                return new PagedResult<RewardsModels.RewardRedemption>(items, response.TotalCount, response.Page, response.PageSize);
            }
        }
        catch { }
        // fallback to legacy
        var legacyQuery = QueryString.Build(new Dictionary<string, string?> { ["status"] = filter.Status is null ? null : Models.RedemptionStatusMap.ToApi(MapLegacyStatus(filter.Status.Value)) });
        var legacy = await http.GetFromJsonAsync<ApiRedemptionsResponse>($"{prefix}/redemptions/all{legacyQuery}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var all = legacy.Redemptions.Select(Map).Select(MapLegacyToV2).ToList();
        if (filter.Type is not null) all = all.Where(r => r.RewardType == filter.Type.Value).ToList();
        if (!string.IsNullOrWhiteSpace(filter.Search)) all = all.Where(r => (r.RewardName?.Contains(filter.Search, StringComparison.OrdinalIgnoreCase) ?? false) || r.PlayerName.Contains(filter.Search, StringComparison.OrdinalIgnoreCase)).ToList();
        var pageItems = all.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
        return new PagedResult<RewardsModels.RewardRedemption>(pageItems, all.Count, filter.Page, filter.PageSize);
    }

    public async Task<RewardsModels.RewardRedemption> GetRedemptionAsync(Guid id, CancellationToken ct = default)
    {
        var item = await http.GetFromJsonAsync<ApiV2Redemption>($"{prefix}/redemptions/{id}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return MapV2(item);
    }

    public async Task<RewardsModels.RewardRedemption> ApproveAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default)
    {
        var payload = new ApiRowVersionRequest(rowVersion, idempotencyKey);
        return await PostRedemptionAsync($"{prefix}/redemptions/{id}/approve", payload, rowVersion, idempotencyKey, ct);
    }

    public async Task<RewardsModels.RewardRedemption> RejectAsync(Guid id, string rowVersion, string idempotencyKey, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ApiErrorException(new ApiErrorView("InvalidRewardData", "Reason required", null, new Dictionary<string, string[]> { ["reason"] = ["Reason required"] }));
        var payload = new ApiRejectRequest(rowVersion, idempotencyKey, reason);
        return await PostRedemptionAsync($"{prefix}/redemptions/{id}/reject", payload, rowVersion, idempotencyKey, ct);
    }

    public async Task<RewardsModels.RewardRedemption> DeliverAsync(Guid id, string rowVersion, string idempotencyKey, CancellationToken ct = default)
    {
        var payload = new ApiRowVersionRequest(rowVersion, idempotencyKey);
        return await PostRedemptionAsync($"{prefix}/redemptions/{id}/deliver", payload, rowVersion, idempotencyKey, ct);
    }

    public async Task<RewardsModels.RewardRedemption> CancelAsync(Guid id, string rowVersion, string idempotencyKey, string? reason, CancellationToken ct = default)
    {
        var payload = new ApiCancelRequest(rowVersion, idempotencyKey, reason);
        return await PostRedemptionAsync($"{prefix}/redemptions/{id}/cancel", payload, rowVersion, idempotencyKey, ct);
    }

    private async Task<RewardsModels.RewardRedemption> PostRedemptionAsync<T>(string url, T payload, string rowVersion, string idempotencyKey, CancellationToken ct)
    {
        // Block normal redemption for Consolation type: client-side guard (T028)
        // Server will also return InvalidRewardType; we surface as ApiError.
        using var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload)
        };
        message.Headers.TryAddWithoutValidation("If-Match", $"W/\"{rowVersion}\"");
        message.Headers.TryAddWithoutValidation("X-Idempotency-Key", idempotencyKey);
        using var response = await http.SendAsync(message, ct);
        // If response is 400 InvalidRewardType for Consolation normal flow, map clearly
        var item = await response.ReadAsAsync<ApiV2Redemption>(ct);
        return MapV2(item);
    }

    private async Task PostAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { }) };
        using var response = await http.SendAsync(request, ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    private static Models.RedemptionSummary Map(ApiRedemption r)
    {
        var decision = r.Transitions
            .Where(t => !string.Equals(t.Status, "REQUESTED", StringComparison.OrdinalIgnoreCase))
            .Select(t => (DateTimeOffset?)t.At)
            .FirstOrDefault();
        return new Models.RedemptionSummary(
            r.Id, r.PlayerId, r.RewardId, r.GameId, r.Points,
            Models.RedemptionStatusMap.FromApi(r.Status), r.RequestedAt,
            decision ?? r.DeliveredAt,
            PlayerName: null, RewardName: null);
    }

    private static RewardsModels.RewardRedemption MapV2(ApiV2Redemption r) => new(
        r.RedemptionId, r.RewardId, r.RewardName, RewardsModels.RewardTypeMap.FromApi(r.RewardType),
        r.PlayerId, r.PlayerName, r.Cost, RewardsModels.RedemptionStateMap.FromApi(r.Status),
        r.RequestedAt, r.ApprovedAt, r.RejectedAt, r.DeliveredAt, r.Reason, r.IsConsolation, r.RowVersion);

    private static RewardsModels.RewardRedemption MapLegacyToV2(Models.RedemptionSummary r) => new(
        r.Id, r.RewardId, r.RewardName ?? r.RewardId.ToString()[..8],
        RewardsModels.RewardType.Physical, r.PlayerId, r.PlayerName ?? r.PlayerId.ToString()[..8],
        r.PointCost, MapLegacyToV2Status(r.Status), r.RequestedAt, null, null, r.DecidedAt, null, false, "legacy");

    private static RewardsModels.RedemptionStateView MapLegacyToV2Status(Models.RedemptionStatusView s) => s switch
    {
        Models.RedemptionStatusView.Pending => RewardsModels.RedemptionStateView.Requested,
        Models.RedemptionStatusView.Approved => RewardsModels.RedemptionStateView.Approved,
        Models.RedemptionStatusView.Rejected => RewardsModels.RedemptionStateView.Rejected,
        Models.RedemptionStatusView.Delivered => RewardsModels.RedemptionStateView.Delivered,
        Models.RedemptionStatusView.Cancelled => RewardsModels.RedemptionStateView.Cancelled,
        _ => RewardsModels.RedemptionStateView.Requested
    };

    private static Models.RedemptionStatusView MapLegacyStatus(RewardsModels.RedemptionStateView s) => s switch
    {
        RewardsModels.RedemptionStateView.Requested => Models.RedemptionStatusView.Pending,
        RewardsModels.RedemptionStateView.Approved => Models.RedemptionStatusView.Approved,
        RewardsModels.RedemptionStateView.Rejected => Models.RedemptionStatusView.Rejected,
        RewardsModels.RedemptionStateView.Delivered => Models.RedemptionStatusView.Delivered,
        RewardsModels.RedemptionStateView.Cancelled => Models.RedemptionStatusView.Cancelled,
        _ => Models.RedemptionStatusView.Pending
    };
}
