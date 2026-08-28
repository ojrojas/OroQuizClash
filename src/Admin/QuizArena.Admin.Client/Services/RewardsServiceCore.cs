using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

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

    public async Task<PagedResult<RewardSummary>> GetRewardsAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        // GET /api/rewards returns the full catalog (player-oriented); paginate client-side.
        var response = await http.GetFromJsonAsync<ApiRewardsResponse>($"{prefix}/rewards?includeUnavailable=true", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        var items = response.Rewards.Select(Map).ToList();
        var pageItems = items.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PagedResult<RewardSummary>(pageItems, items.Count, page, pageSize);
    }

    public async Task<RewardSummary> CreateRewardAsync(RewardForm form, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"{prefix}/rewards", ToRequest(form), ct);
        return Map(await response.ReadAsAsync<ApiRewardItem>(ct));
    }

    public async Task<RewardSummary> UpdateRewardAsync(Guid rewardId, RewardForm form, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"{prefix}/rewards/{rewardId}", ToRequest(form), ct);
        return Map(await response.ReadAsAsync<ApiRewardItem>(ct));
    }

    public Task ActivateRewardAsync(Guid rewardId, CancellationToken ct = default) =>
        PostAsync($"{prefix}/rewards/{rewardId}/activate", ct);

    public Task DeactivateRewardAsync(Guid rewardId, CancellationToken ct = default) =>
        PostAsync($"{prefix}/rewards/{rewardId}/deactivate", ct);

    private async Task PostAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { }) };
        using var response = await http.SendAsync(request, ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    private static ApiRewardRequest ToRequest(RewardForm form) =>
        new(form.Name.Trim(), form.Description, form.PointCost, form.Stock ?? 0, form.ExpirationDate);

    private static RewardSummary Map(ApiRewardItem r) => new(
        r.Id, r.Name, r.Description, r.PointsRequired, r.Stock,
        RewardStatusMap.FromApi(r.Status), r.ExpirationDate, r.Available);
}
