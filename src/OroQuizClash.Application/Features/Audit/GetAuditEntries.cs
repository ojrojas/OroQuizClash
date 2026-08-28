using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Application.Authorization;
using OroQuizClash.Domain.Audit;
using OroQuizClash.Domain.Authorization;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Audit;

[RequiresPermission("Audit.Read")]
public sealed record GetAuditEntriesQuery(
    string? CorrelationId,
    string? ActorId,
    string? Action,
    string? Resource,
    string? Result,
    DateTimeOffset? From,
    DateTimeOffset? To,
    int Page = 1,
    int PageSize = 20) : IQuery<Result<GetAuditEntriesResponse>>;

public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset Timestamp,
    string ActorId,
    string ActorRoles,
    string Action,
    string Permission,
    string Resource,
    string CorrelationId,
    string? TenantId,
    string Result,
    string? Reason,
    string? Details);

public sealed record GetAuditEntriesResponse(
    IReadOnlyList<AuditEntryResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed class GetAuditEntriesHandler(
    IRepository<AuditEntry, Guid> repository) : IQueryHandler<GetAuditEntriesQuery, Result<GetAuditEntriesResponse>>
{
    public async Task<Result<GetAuditEntriesResponse>> HandleAsync(GetAuditEntriesQuery query, CancellationToken ct)
    {
        var spec = new AuditEntrySpecification(query.CorrelationId, query.ActorId, query.Action, query.Resource, query.Result, query.From, query.To, query.Page, query.PageSize);
        var items = await repository.ListAsync(spec, ct);
        // For simplicity, total is items count (paginated). Real count would require CountAsync.
        var total = items.Count;
        if (items.Count == query.PageSize)
        {
            // Approximate total if page is full — fetch next page to detect more
            var nextSpec = new AuditEntrySpecification(query.CorrelationId, query.ActorId, query.Action, query.Resource, query.Result, query.From, query.To, query.Page + 1, 1);
            var next = await repository.ListAsync(nextSpec, ct);
            if (next.Count > 0) total = query.Page * query.PageSize + 1;
        }

        var responses = items.Select(e => new AuditEntryResponse(e.Id, e.Timestamp, e.ActorId, e.ActorRoles, e.Action, e.Permission, e.Resource, e.CorrelationId, e.TenantId, e.Result, e.Reason, e.Details)).ToList();
        return Result.Success(new GetAuditEntriesResponse(responses, query.Page, query.PageSize, total));
    }
}

public sealed record GetAuditEntryByIdQuery(Guid Id) : IQuery<Result<AuditEntryResponse>>;

public sealed class GetAuditEntryByIdHandler(IRepository<AuditEntry, Guid> repository) : IQueryHandler<GetAuditEntryByIdQuery, Result<AuditEntryResponse>>
{
    public async Task<Result<AuditEntryResponse>> HandleAsync(GetAuditEntryByIdQuery query, CancellationToken ct)
    {
        var entry = await repository.GetByIdAsync(query.Id, ct);
        if (entry is null) return Result.Failure<AuditEntryResponse>(Error.NotFound("Audit.NotFound", "Audit entry not found."));
        return Result.Success(new AuditEntryResponse(entry.Id, entry.Timestamp, entry.ActorId, entry.ActorRoles, entry.Action, entry.Permission, entry.Resource, entry.CorrelationId, entry.TenantId, entry.Result, entry.Reason, entry.Details));
    }
}

public sealed class GetAuditEntriesEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit", async (
            string? correlationId,
            string? actorId,
            string? action,
            string? resource,
            string? result,
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetAuditEntriesQuery(correlationId, actorId, action, resource, result, from, to, page ?? 1, pageSize ?? 20);
            var res = await sender.SendAsync(query, ct);
            return res.ToHttpResult();
        }).RequireAuthorization("Audit.Read").RequireRateLimiting("ReadLimiter");

        app.MapGet("/api/audit/{id:guid}", async (
            Guid id,
            ISender sender,
            CancellationToken ct) =>
        {
            var res = await sender.SendAsync(new GetAuditEntryByIdQuery(id), ct);
            return res.ToHttpResult();
        }).RequireAuthorization("Audit.Read").RequireRateLimiting("ReadLimiter");
    }
}
