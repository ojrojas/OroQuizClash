using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetScoreLedgerQuery(
    Guid GameId,
    Guid PlayerId,
    int Page = 1,
    int PageSize = 50,
    string? Type = null) : IQuery<Result<LedgerPageResponse>>;

public sealed record LedgerPageResponse(
    IReadOnlyList<LedgerEntryResponse> Items,
    int TotalCount,
    int Page,
    int PageSize,
    bool HasMore);

public sealed record LedgerEntryResponse(
    Guid Id,
    string Type,
    int Points,
    int ResultingBalance,
    Guid? RoundId,
    Guid? QuestionId,
    string? Reason,
    DateTimeOffset CreatedAt);

public sealed class GetScoreLedgerHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetScoreLedgerQuery, Result<LedgerPageResponse>>
{
    public async Task<Result<LedgerPageResponse>> HandleAsync(GetScoreLedgerQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<LedgerPageResponse>(GameErrors.GameNotFound);

        var player = game.Players.FirstOrDefault(p => p.UserId == query.PlayerId);
        if (player is null) return Result.Failure<LedgerPageResponse>(GameErrors.PlayerNotInGame);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        IEnumerable<PointTransaction> transactions = game.PointTransactions
            .Where(pt => pt.PlayerId == query.PlayerId);

        if (!string.IsNullOrWhiteSpace(query.Type))
        {
            var typeFilter = PointTransactionType.GetAll()
                .FirstOrDefault(t => string.Equals(t.Name, query.Type, StringComparison.OrdinalIgnoreCase));
            if (typeFilter is null)
                return Result.Failure<LedgerPageResponse>(GameErrors.InvalidGameConfiguration($"Unknown transaction type: {query.Type}"));
            transactions = transactions.Where(pt => pt.Type == typeFilter);
        }

        var ordered = transactions.OrderByDescending(pt => pt.CreatedAt).ToList();
        var totalCount = ordered.Count;
        var items = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(pt => new LedgerEntryResponse(
                pt.Id.Value,
                pt.Type.Name,
                pt.Points,
                pt.ResultingBalance,
                pt.RoundId?.Value,
                pt.QuestionId?.Value,
                pt.Reason,
                pt.CreatedAt))
            .ToList();

        return Result.Success(new LedgerPageResponse(
            items,
            totalCount,
            page,
            pageSize,
            page * pageSize < totalCount));
    }
}

public sealed class GetScoreLedgerEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}/score/{playerId:guid}/ledger", async (
            Guid id,
            Guid playerId,
            int? page,
            int? pageSize,
            string? type,
            ISender sender,
            CancellationToken ct) =>
        {
            var query = new GetScoreLedgerQuery(id, playerId, page ?? 1, pageSize ?? 50, type);
            var result = await sender.SendAsync(query, ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
