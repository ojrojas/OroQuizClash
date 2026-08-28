using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

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

    public async Task<PagedResult<RedemptionSummary>> GetRedemptionsAsync(RedemptionFilter filter, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["status"] = filter.Status is null ? null : RedemptionStatusMap.ToApi(filter.Status.Value)
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

    private async Task PostAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { }) };
        using var response = await http.SendAsync(request, ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    private static RedemptionSummary Map(ApiRedemption r)
    {
        var decision = r.Transitions
            .Where(t => !string.Equals(t.Status, "REQUESTED", StringComparison.OrdinalIgnoreCase))
            .Select(t => (DateTimeOffset?)t.At)
            .FirstOrDefault();
        return new RedemptionSummary(
            r.Id, r.PlayerId, r.RewardId, r.GameId, r.Points,
            RedemptionStatusMap.FromApi(r.Status), r.RequestedAt,
            decision ?? r.DeliveredAt,
            PlayerName: null, RewardName: null);
    }
}
