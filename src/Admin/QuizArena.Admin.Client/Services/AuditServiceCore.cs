using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;
using AuditModels = QuizArena.Admin.Client.Models.Audit;

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

    private sealed record ApiAuditV2Entry(
        Guid AuditId,
        string ActorId,
        string DisplayName,
        string Email,
        string? TenantId,
        string What,
        DateTimeOffset When,
        string Service,
        string Endpoint,
        string? IpAddress,
        string CorrelationId,
        string? TraceId,
        string EntityType,
        Guid EntityId,
        string? PreviousValue,
        string? NewValue,
        string Action,
        string Status,
        string? ErrorCode,
        string? Detail);

    private sealed record ApiAuditV2Page(IReadOnlyList<ApiAuditV2Entry> Items, int TotalCount, int Page, int PageSize);
    private sealed record ApiAuditV2Detail(
        Guid AuditId,
        string ActorId,
        string DisplayName,
        string Email,
        string? TenantId,
        string What,
        DateTimeOffset When,
        string Service,
        string Endpoint,
        string? IpAddress,
        string CorrelationId,
        string? TraceId,
        string EntityType,
        Guid EntityId,
        string? PreviousValue,
        string? NewValue,
        string Action,
        string Status,
        string? ErrorCode,
        string? Detail,
        IReadOnlyList<ApiDiffEntry>? Diff);
    private sealed record ApiDiffEntry(string Path, string? Previous, string? New, string ChangeType);

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

    // 026 Admin Audit — 9 campos
    public async Task<PagedResult<AuditModels.AuditEntry>> GetAuditAsync(AuditModels.AuditFilter filter, CancellationToken ct = default)
    {
        var validation = filter.Validate();
        if (validation.Count > 0) throw new ApiErrorException(new ApiErrorView("InvalidFilter", "Invalid filter", null, validation));
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["who"] = filter.Who,
            ["what"] = filter.What,
            ["whenFrom"] = filter.WhenFrom?.ToString("O"),
            ["whenTo"] = filter.WhenTo?.ToString("O"),
            ["where"] = filter.Where,
            ["entityType"] = filter.EntityType,
            ["entityId"] = filter.EntityId?.ToString(),
            ["action"] = filter.Action,
            ["result"] = filter.Result,
            ["errorCode"] = filter.ErrorCode,
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        try
        {
            var result = await http.GetFromJsonAsync<ApiAuditV2Page>($"{prefix}/audit{query}", ct)
                ?? throw new ApiErrorException(ApiErrorView.Unknown);
            return new PagedResult<AuditModels.AuditEntry>(result.Items.Select(MapV2).ToList(), result.TotalCount, result.Page, result.PageSize);
        }
        catch
        {
            // Fallback to legacy audit page and map to new model
            var legacyQuery = QueryString.Build(new Dictionary<string, string?>
            {
                ["actorId"] = filter.Who,
                ["action"] = filter.Action,
                ["from"] = filter.WhenFrom?.ToString("O"),
                ["to"] = filter.WhenTo?.ToString("O"),
                ["page"] = filter.Page.ToString(),
                ["pageSize"] = filter.PageSize.ToString()
            });
            var legacy = await http.GetFromJsonAsync<ApiAuditPage>($"{prefix}/audit{legacyQuery}", ct)
                ?? throw new ApiErrorException(ApiErrorView.Unknown);
            var items = legacy.Items.Select(MapLegacyToV2).ToList();
            if (!string.IsNullOrWhiteSpace(filter.What)) items = items.Where(e => e.What.Contains(filter.What, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(filter.Where)) items = items.Where(e => e.Where.Service.Contains(filter.Where, StringComparison.OrdinalIgnoreCase) || e.Where.CorrelationId.Contains(filter.Where, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(filter.EntityType)) items = items.Where(e => e.Entity.EntityType.Equals(filter.EntityType, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!string.IsNullOrWhiteSpace(filter.Result)) items = items.Where(e => e.Result.Status.Equals(filter.Result, StringComparison.OrdinalIgnoreCase)).ToList();
            var pageItems = items.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToList();
            return new PagedResult<AuditModels.AuditEntry>(pageItems, items.Count, filter.Page, filter.PageSize);
        }
    }

    public async Task<AuditModels.AuditDetail> GetAuditDetailAsync(Guid id, AuditModels.AuditFilter? contextFilter, CancellationToken ct = default) =>
        await GetDetailAsync(id, ct);

    public async Task<AuditModels.AuditDetail> GetDetailAsync(Guid auditId, CancellationToken ct = default)
    {
        try
        {
            var result = await http.GetFromJsonAsync<ApiAuditV2Detail>($"{prefix}/audit/{auditId}", ct)
                ?? throw new ApiErrorException(ApiErrorView.Unknown);
            return MapDetail(result);
        }
        catch
        {
            var legacy = await http.GetFromJsonAsync<ApiAuditEntry>($"{prefix}/audit/{auditId}", ct)
                ?? throw new ApiErrorException(ApiErrorView.Unknown);
            var mapped = MapLegacyToV2(legacy);
            return new AuditModels.AuditDetail(mapped.AuditId, mapped.Who, mapped.What, mapped.When, mapped.Where, mapped.Entity, mapped.PreviousValue, mapped.NewValue, mapped.Action, mapped.Result, []);
        }
    }

    private static AuditModels.AuditEntry MapV2(ApiAuditV2Entry e) => new(
        e.AuditId,
        new AuditModels.WhoView(e.ActorId, e.DisplayName, e.Email, e.TenantId),
        e.What, e.When,
        new AuditModels.WhereView(e.Service, e.Endpoint, e.IpAddress, e.CorrelationId, e.TraceId),
        new AuditModels.EntityView(e.EntityType, e.EntityId),
        e.PreviousValue, e.NewValue, e.Action,
        new AuditModels.ResultView(e.Status, e.ErrorCode, e.Detail));

    private static AuditModels.AuditDetail MapDetail(ApiAuditV2Detail e) => new(
        e.AuditId,
        new AuditModels.WhoView(e.ActorId, e.DisplayName, e.Email, e.TenantId),
        e.What, e.When,
        new AuditModels.WhereView(e.Service, e.Endpoint, e.IpAddress, e.CorrelationId, e.TraceId),
        new AuditModels.EntityView(e.EntityType, e.EntityId),
        e.PreviousValue, e.NewValue, e.Action,
        new AuditModels.ResultView(e.Status, e.ErrorCode, e.Detail),
        e.Diff?.Select(d => new AuditModels.JsonDiffEntry(d.Path, d.Previous, d.New, d.ChangeType)).ToList() ?? []);

    private static AuditModels.AuditEntry MapLegacyToV2(ApiAuditEntry e) => new(
        e.Id,
        new AuditModels.WhoView(e.ActorId, e.ActorId, e.ActorId, e.TenantId),
        e.Action, e.Timestamp,
        new AuditModels.WhereView(e.Resource, e.Resource, null, e.CorrelationId, null),
        new AuditModels.EntityView(e.Resource, Guid.TryParse(e.ResourceId, out var g) ? g : e.GameId ?? e.Id),
        e.Data, e.Details, e.Action,
        new AuditModels.ResultView(e.Result, e.Reason, e.Details));
}
