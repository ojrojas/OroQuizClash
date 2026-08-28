using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Games;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Reporting;

public sealed record GetLeaderboardExtendedQuery(
    Guid? GameId = null,
    Guid? CategoryId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<Result<LeaderboardResponse>>;

public sealed class GetLeaderboardExtendedHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetLeaderboardExtendedQuery, Result<LeaderboardResponse>>
{
    public async Task<Result<LeaderboardResponse>> HandleAsync(GetLeaderboardExtendedQuery query, CancellationToken ct)
    {
        if (query.From.HasValue && query.To.HasValue && query.From.Value > query.To.Value)
            return Result.Failure<LeaderboardResponse>(Error.Validation("Reporting.InvalidPeriod", "from must be <= to"));

        IReadOnlyList<Game> games;
        if (query.GameId.HasValue)
        {
            var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId.Value));
            var game = await repository.FirstOrDefaultAsync(spec, ct);
            if (game is null) return Result.Failure<LeaderboardResponse>(Domain.Shared.Errors.GameErrors.GameNotFound);
            games = [game];
        }
        else if (query.CategoryId.HasValue)
        {
            var spec = new ReportingGamesByCategorySpecification(query.CategoryId.Value, query.From, query.To);
            games = await repository.ListAsync(spec, ct);
        }
        else
        {
            var spec = new GamesByPeriodSpecification(query.From, query.To);
            games = await repository.ListAsync(spec, ct);
        }

        // For Global/Category/Period, aggregate points across games
        // Simplest: build leaderboard from first game if single, otherwise aggregate
        if (games.Count == 1)
        {
            var singleGame = games[0];
            // If period filter, we should filter PointTransactions by period before building
            // For now, reuse existing builder (period filtering is best-effort via Game.CreatedAt already in spec)
            return Result.Success(new LeaderboardResponse(singleGame.Id.Value, LeaderboardBuilder.Build(singleGame)));
        }

        // Global aggregation: sum points per player across games
        var aggregated = games.SelectMany(g => LeaderboardBuilder.Build(g))
            .GroupBy(e => e.PlayerId)
            .Select(g => new LeaderboardEntryResponse(
                g.Key,
                g.First().DisplayName,
                0,
                g.Sum(e => e.Points),
                g.Sum(e => e.CorrectAnswers),
                g.Max(e => e.CurrentLevel),
                g.First().Status,
                g.Sum(e => e.SecuredPoints)))
            .OrderByDescending(e => e.Points)
            .ThenByDescending(e => e.CorrectAnswers)
            .Select((e, idx) => e with { Rank = idx + 1 })
            .ToList();

        return Result.Success(new LeaderboardResponse(query.GameId ?? Guid.Empty, aggregated));
    }
}

public sealed class GamesByPeriodSpecification : BuildingBlocks.Kernel.Domain.Specifications.Specification<Game>
{
    public GamesByPeriodSpecification(DateTimeOffset? from, DateTimeOffset? to)
    {
        ApplyAsNoTracking();
        if (from.HasValue) Where(g => g.CreatedAt >= from.Value);
        if (to.HasValue) Where(g => g.CreatedAt <= to.Value);
    }
}

public sealed class LeaderboardExtendedEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/leaderboard", async (
            Guid? gameId,
            Guid? categoryId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetLeaderboardExtendedQuery(gameId, categoryId, from, to), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("Report.Read");
    }
}
