using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Reporting;

public sealed record GetPlayerReportQuery(
    Guid PlayerId,
    Guid? GameId = null,
    Guid? CategoryId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<Result<PlayerReportResponse>>;

public sealed record PlayerReportResponse(
    Guid PlayerId,
    int GamesPlayed,
    int GamesWon,
    int GamesLost,
    int GamesWithdrawn,
    int QuestionsAnswered,
    int CorrectAnswers,
    double? Accuracy,
    int PointsEarned,
    int PointsRedeemed);

public sealed class GetPlayerReportValidator : IValidator<GetPlayerReportQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(GetPlayerReportQuery request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.PlayerId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.PlayerId), "PlayerId required."));
        if (request.From.HasValue && request.To.HasValue && request.From.Value > request.To.Value)
            failures.Add(new ValidationFailure(nameof(request.From), "from must be <= to"));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class GetPlayerReportHandler(IRepository<Game, GameId> gameRepository) : IQueryHandler<GetPlayerReportQuery, Result<PlayerReportResponse>>
{
    public async Task<Result<PlayerReportResponse>> HandleAsync(GetPlayerReportQuery query, CancellationToken ct)
    {
        // Load games where player participated, filtered by GameId/CategoryId/Period
        IReadOnlyList<Game> games;
        if (query.GameId.HasValue)
        {
            var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId.Value));
            var game = await gameRepository.FirstOrDefaultAsync(spec, ct);
            games = game is not null ? [game] : [];
        }
        else if (query.CategoryId.HasValue)
        {
            var spec = new ReportingGamesByCategorySpecification(query.CategoryId.Value, query.From, query.To);
            games = await gameRepository.ListAsync(spec, ct);
            games = games.Where(g => g.Players.Any(p => p.UserId == query.PlayerId)).ToList();
        }
        else
        {
            var spec = new GamesByPeriodSpecification(query.From, query.To);
            games = await gameRepository.ListAsync(spec, ct);
            games = games.Where(g => g.Players.Any(p => p.UserId == query.PlayerId)).ToList();
        }

        var finishedGames = games.Where(g => g.Status.Name == "FINISHED").ToList();
        var gamesPlayed = finishedGames.Count;
        var gamesWithdrawn = games.Count(g => g.Players.FirstOrDefault(p => p.UserId == query.PlayerId)?.ParticipationStatus.Name == "WITHDRAWN");
        var gamesWon = finishedGames.Count(g =>
        {
            var lb = Features.Games.LeaderboardBuilder.Build(g);
            return lb.FirstOrDefault()?.PlayerId == query.PlayerId;
        });
        var gamesLost = gamesPlayed - gamesWon - gamesWithdrawn;
        if (gamesLost < 0) gamesLost = 0;

        var questionsAnswered = 0;
        var correctAnswers = 0;
        var pointsEarned = 0;
        var pointsRedeemed = 0;

        foreach (var game in games)
        {
            var playerAnswers = game.Answers.Where(a => a.PlayerId == query.PlayerId && a.Status.Name == "EVALUATED").ToList();
            // Filter by period if needed
            if (query.From.HasValue) playerAnswers = playerAnswers.Where(a => a.CreatedAt >= query.From.Value).ToList();
            if (query.To.HasValue) playerAnswers = playerAnswers.Where(a => a.CreatedAt <= query.To.Value).ToList();
            // Filter by category if needed (via question's category) - simplified: already filtered games by category
            questionsAnswered += playerAnswers.Count;
            correctAnswers += playerAnswers.Count(a => a.Correct == true);
            pointsEarned += game.PointTransactions.Where(p => p.PlayerId == query.PlayerId && (p.Type.Name == "ANSWER_CORRECT" || p.Type.Name == "ROUND_BONUS")).Sum(p => p.Points);
            // PointsRedeemed would come from RewardRedemption, but for now 0 (extend when Reward repo available)
        }

        var accuracy = questionsAnswered == 0 ? (double?)null : Math.Round((double)correctAnswers / questionsAnswered * 100, 2);

        return Result.Success(new PlayerReportResponse(
            query.PlayerId,
            gamesPlayed,
            gamesWon,
            gamesLost,
            gamesWithdrawn,
            questionsAnswered,
            correctAnswers,
            accuracy,
            pointsEarned,
            pointsRedeemed));
    }
}

public sealed class GetPlayerReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/players/{playerId:guid}", async (
            Guid playerId,
            Guid? gameId,
            Guid? categoryId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetPlayerReportQuery(playerId, gameId, categoryId, from, to), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("Report.Read");
    }
}
