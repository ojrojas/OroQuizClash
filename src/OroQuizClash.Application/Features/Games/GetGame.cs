using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Games;

public sealed record GetGameQuery(Guid GameId) : IQuery<Result<GameResponse>>;

public sealed class GetGameHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetGameQuery, Result<GameResponse>>
{
    public async Task<Result<GameResponse>> HandleAsync(GetGameQuery query, CancellationToken ct)
    {
        var spec = new GameByIdSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<GameResponse>(GameErrors.GameNotFound);
        return Result.Success(new GameResponse(
            game.Id.Value, game.Name, game.Status.Name, game.Configuration.CategoryId.Value,
            game.Configuration.MinRounds, game.Configuration.MaxRounds,
            game.Players.Count, game.Rounds.Count,
            game.RowVersion != null && game.RowVersion.Length > 0 ? Convert.ToBase64String(game.RowVersion) : string.Empty,
            game.CreatedAt, game.ReadyAt, game.StartedAt, game.FinishedAt));
    }
}

public sealed record GetGamesQuery(string? Status, Guid? CategoryId, Guid? CreatedBy, string? Search, int Page = 1, int PageSize = 20) : IQuery<Result<PaginatedGamesResponse>>;

public sealed record PaginatedGamesResponse(IReadOnlyList<GameResponse> Items, int Total, int Page, int PageSize);

public sealed class GetGamesHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetGamesQuery, Result<PaginatedGamesResponse>>
{
    public async Task<Result<PaginatedGamesResponse>> HandleAsync(GetGamesQuery query, CancellationToken ct)
    {
        var spec = new GameFilterSpecification(query.Status, query.CategoryId, query.CreatedBy, query.Search, query.Page, query.PageSize, true);
        var countSpec = new GameFilterSpecification(query.Status, query.CategoryId, query.CreatedBy, query.Search, 1, 20, false);
        var items = await repository.ListAsync(spec, ct);
        var total = await repository.CountAsync(countSpec, ct);
        var mapped = items.Select(g => new GameResponse(
            g.Id.Value, g.Name, g.Status.Name, g.Configuration.CategoryId.Value,
            g.Configuration.MinRounds, g.Configuration.MaxRounds,
            g.Players.Count, g.Rounds.Count,
            g.RowVersion != null && g.RowVersion.Length > 0 ? Convert.ToBase64String(g.RowVersion) : string.Empty,
            g.CreatedAt, g.ReadyAt, g.StartedAt, g.FinishedAt)).ToList();
        return Result.Success(new PaginatedGamesResponse(mapped, total, query.Page, query.PageSize));
    }
}

public sealed class GetGameEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{id:guid}", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetGameQuery(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();

        app.MapGet("/api/games", async (string? status, Guid? categoryId, Guid? createdBy, string? search, int? page, int? pageSize, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetGamesQuery(status, categoryId, createdBy, search, page ?? 1, pageSize ?? 20), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
