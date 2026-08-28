using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public class AuditServiceCore(HttpClient http, string prefix) : IAuditService
{
    private sealed record ApiAuditEntry(
        Guid Id,
        DateTimeOffset Timestamp,
        string ActorId,
        string ActorRoles,
        string Action,
        string Permission,
        string Resource,
        string? ResourceId,
        Guid? GameId,
        Guid? PlayerId,
        string CorrelationId,
        string? TenantId,
        string Result,
        string? Reason,
        string? Details,
        string? Data);

    private sealed record ApiAuditPage(IReadOnlyList<ApiAuditEntry> Items, int Page, int PageSize, int Total);

    public async Task<PagedResult<AuditEntry>> GetAuditAsync(AuditFilter filter, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["actorId"] = filter.ActorId,
            ["action"] = filter.Action,
            ["from"] = filter.From?.ToString("O"),
            ["to"] = filter.To?.ToString("O"),
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var result = await http.GetFromJsonAsync<ApiAuditPage>($"{prefix}/audit{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new PagedResult<AuditEntry>(
            result.Items.Select(Map).ToList(), result.Total, result.Page, result.PageSize);
    }

    public async Task<AuditEntry> GetAuditDetailAsync(Guid id, CancellationToken ct = default) =>
        Map(await http.GetFromJsonAsync<ApiAuditEntry>($"{prefix}/audit/{id}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown));

    private static AuditEntry Map(ApiAuditEntry e) => new(
        e.Id, e.Timestamp, e.ActorId, e.ActorRoles, e.Action, e.Permission,
        e.Resource, e.ResourceId, e.GameId, e.PlayerId, e.CorrelationId,
        e.Result, e.Reason, e.Details, e.Data);
}
