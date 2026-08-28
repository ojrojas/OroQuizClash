using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Shared.Errors;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Reporting;

public sealed record GetGameReportQuery(Guid GameId) : IQuery<Result<GameReportResponse>>;

public sealed record GameReportResponse(
    Guid GameId,
    string Name,
    DateTimeOffset Start,
    DateTimeOffset? End,
    IReadOnlyList<GameReportPlayer> Players,
    IReadOnlyList<GameReportRound> Rounds,
    GameReportWinner? Winner,
    int TotalQuestions,
    int TotalRounds);

public sealed record GameReportPlayer(Guid PlayerId, string? DisplayName, string Status);
public sealed record GameReportRound(Guid RoundId, int RoundNumber, Guid QuestionId);
public sealed record GameReportWinner(Guid PlayerId, string? DisplayName);

public sealed class GetGameReportValidator : IValidator<GetGameReportQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(GetGameReportQuery request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.GameId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.GameId), "GameId required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class GetGameReportHandler(IRepository<Game, GameId> repository) : IQueryHandler<GetGameReportQuery, Result<GameReportResponse>>
{
    public async Task<Result<GameReportResponse>> HandleAsync(GetGameReportQuery query, CancellationToken ct)
    {
        var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId));
        var game = await repository.FirstOrDefaultAsync(spec, ct);
        if (game is null) return Result.Failure<GameReportResponse>(GameErrors.GameNotFound);

        var leaderboard = Features.Games.LeaderboardBuilder.Build(game);
        var winnerEntry = leaderboard.FirstOrDefault();
        var winner = winnerEntry is not null && game.Status.Name == "FINISHED"
            ? new GameReportWinner(winnerEntry.PlayerId, winnerEntry.DisplayName)
            : null;

        var response = new GameReportResponse(
            game.Id.Value,
            game.Name,
            game.CreatedAt,
            game.FinishedAt,
            game.Players.Select(p => new GameReportPlayer(p.UserId, p.DisplayName, p.ParticipationStatus.Name)).ToList(),
            game.Rounds.Select(r => new GameReportRound(r.Id.Value, r.RoundNumber, r.QuestionId.Value)).ToList(),
            winner,
            game.Rounds.Count,
            game.Rounds.Count);

        return Result.Success(response);
    }
}

public sealed class GetGameReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/games/{gameId:guid}", async (
            Guid gameId,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetGameReportQuery(gameId), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("Report.Read");
    }
}
