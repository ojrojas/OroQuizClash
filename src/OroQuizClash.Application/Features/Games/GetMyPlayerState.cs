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

public sealed record GetMyPlayerStateQuery(Guid GameId, Guid PlayerId) : IQuery<Result<PlayerGameStateResponse>>;

public sealed record PlayerGameStateResponse(
    PlayerDto Player,
    GameDto Game,
    GameSessionDto GameSession,
    RoundDto? Round,
    QuestionDto? Question,
    AnswerDto? Answer,
    ScoreDto Score,
    SecuredPointsDto SecuredPoints,
    TimerDto Timer,
    StatusDto Status
);

public sealed record PlayerDto(string PlayerId, string DisplayName, string Email, string? TenantId, string[] Roles, bool MustChangePassword);
public sealed record GameDto(string GameId, string Name, string Status, string CategoryId, string CategoryName, object Configuration, int MaxPlayers, int MinPlayers);
public sealed record GameSessionDto(string GameSessionId, string PlayerId, string GameId, string Status, string JoinedAt, int CurrentRoundNumber, string Version);
public sealed record RoundDto(string RoundId, string GameId, int RoundNumber, string Level, string Status, string QuestionId, string StartedAt, string ExpiresAt, string Version);
public sealed record QuestionDto(string QuestionId, string CategoryId, string Text, AnswerOptionDto[] AnswerOptions, string Difficulty);
public sealed record AnswerOptionDto(string OptionId, string Text);
public sealed record AnswerDto(string? AnswerId, string PlayerId, string GameId, string RoundId, string QuestionId, string? SelectedOptionId, string? SubmittedAt, string State, bool? IsCorrect, string IdempotencyKey);
public sealed record ScoreDto(string PlayerId, string GameId, int TotalPoints, int CorrectAnswers, string CurrentLevel);
public sealed record SecuredPointsDto(string PlayerId, string GameId, int SecuredPoints, int? CheckpointRoundNumber, string Policy);
public sealed record TimerDto(int TimeLimitSeconds, string ExpiresAt, int RemainingSeconds, string State, string ServerNow);
public sealed record StatusDto(string GameStatus, string PlayerStatus, bool IsTerminal, bool CanAnswer);

public sealed class GetMyPlayerStateHandler(IRepository<Game, GameId> repository, IRepository<Domain.Questions.Question, Domain.Questions.QuestionId> questionRepo) : IQueryHandler<GetMyPlayerStateQuery, Result<PlayerGameStateResponse>>
{
    public async Task<Result<PlayerGameStateResponse>> HandleAsync(GetMyPlayerStateQuery query, CancellationToken ct)
    {
        var game = await repository.FirstOrDefaultAsync(new GameByIdWithAnswersSpecification(new GameId(query.GameId)), ct);
        if (game is null) return Result.Failure<PlayerGameStateResponse>(GameErrors.GameNotFound);

        var player = game.Players.FirstOrDefault(p => p.UserId == query.PlayerId);
        if (player is null) return Result.Failure<PlayerGameStateResponse>(GameErrors.PlayerNotInGame);

        var currentRound = game.CurrentRound ?? game.Rounds.OrderByDescending(r => r.RoundNumber).FirstOrDefault();
        Domain.Questions.Question? question = null;
        if (currentRound is not null)
        {
            question = await questionRepo.FirstOrDefaultAsync(new QuestionByIdSpecification(currentRound.QuestionId), ct);
        }

        var answer = currentRound is not null ? game.Answers.FirstOrDefault(a => a.PlayerId == query.PlayerId && a.RoundId == currentRound.Id) : null;

        var serverNow = DateTimeOffset.UtcNow;
        var expiresAt = currentRound is not null ? currentRound.StartedAt.AddSeconds(currentRound.TimeLimit) : serverNow.AddSeconds(game.Configuration.TimeLimitPerQuestionSeconds);
        var remaining = Math.Max(0, (int)(expiresAt - serverNow).TotalSeconds);
        var timerState = currentRound?.Status.Name == "ROUND_IN_PROGRESS" && remaining > 0 ? "RUNNING" : remaining == 0 && currentRound?.Status.Name == "ROUND_IN_PROGRESS" ? "EXPIRED" : "STOPPED";
        var isTerminal = player.ParticipationStatus.Name != "ACTIVE" || game.Status.IsTerminal;
        var canAnswer = !isTerminal && currentRound?.Status.Name == "ROUND_IN_PROGRESS" && (answer is null || answer.Status.Name == "NOT_ANSWERED" || answer.Status.Name == "PENDING");

        // Filter isCorrect for PLAYER: only expose when EVALUATED (Server Truth V)
        bool? exposedIsCorrect = null;
        string? exposedSelected = null;
        string answerStateName = "PENDING";
        if (answer is not null)
        {
            answerStateName = answer.Status.Name;
            exposedSelected = answer.AnswerOptionId.Value.ToString();
            if (answerStateName == "EVALUATED" || answerStateName == "CORRECT" || answerStateName == "INCORRECT")
                exposedIsCorrect = answer.Correct;
            else
                exposedIsCorrect = null;
        }

        var response = new PlayerGameStateResponse(
            new PlayerDto(query.PlayerId.ToString(), player.DisplayName ?? "Player", "", null, Array.Empty<string>(), false),
            new GameDto(game.Id.Value.ToString(), game.Name, game.Status.Name, game.Configuration.CategoryId.Value.ToString(), "", game.Configuration, game.Configuration.MaxPlayers, game.Configuration.MinPlayers),
            new GameSessionDto(player.Id.Value.ToString(), query.PlayerId.ToString(), game.Id.Value.ToString(), player.ParticipationStatus.Name, player.JoinedAt.ToString("O"), player.CurrentRoundNumber, Convert.ToBase64String(game.RowVersion ?? Array.Empty<byte>())),
            currentRound is null ? null : new RoundDto(currentRound.Id.Value.ToString(), game.Id.Value.ToString(), currentRound.RoundNumber, currentRound.Difficulty.ToString(), currentRound.Status.Name, currentRound.QuestionId.Value.ToString(), currentRound.StartedAt.ToString("O"), expiresAt.ToString("O"), ""),
            question is null ? null : new QuestionDto(question.Id.Value.ToString(), question.CategoryId.Value.ToString(), question.Text, question.AnswerOptions.Select(o => new AnswerOptionDto(o.Id.Value.ToString(), o.Text)).ToArray(), question.Difficulty.Name),
            answer is null ? new AnswerDto(null, query.PlayerId.ToString(), game.Id.Value.ToString(), currentRound?.Id.Value.ToString() ?? "", currentRound?.QuestionId.Value.ToString() ?? "", null, null, "PENDING", null, "") : new AnswerDto(answer.Id.Value.ToString(), query.PlayerId.ToString(), game.Id.Value.ToString(), answer.RoundId.Value.ToString(), answer.QuestionId.Value.ToString(), exposedSelected, answer.EvaluatedAt?.ToString("O") ?? answer.CreatedAt.ToString("O"), answerStateName, exposedIsCorrect, answer.Id.Value.ToString()),
            new ScoreDto(query.PlayerId.ToString(), game.Id.Value.ToString(), player.Score.CurrentPoints, game.Answers.Count(a => a.PlayerId == query.PlayerId && a.Correct == true), player.Score.CurrentPoints.ToString()),
            new SecuredPointsDto(query.PlayerId.ToString(), game.Id.Value.ToString(), player.Score.SecuredPoints, player.Score.SecuredPoints > 0 ? player.CurrentRoundNumber : null, game.Configuration.WithdrawalPolicy.Name),
            new TimerDto(game.Configuration.TimeLimitPerQuestionSeconds, expiresAt.ToString("O"), remaining, timerState, serverNow.ToString("O")),
            new StatusDto(game.Status.Name, player.ParticipationStatus.Name, isTerminal, canAnswer)
        );

        return Result.Success(response);
    }
}

public sealed class GetMyPlayerStateEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/games/{gameId:guid}/players/me", async (
            Guid gameId,
            HttpContext http,
            ISender sender,
            CancellationToken ct) =>
        {
            var sub = GameClaims.GetSub(http.User);
            if (sub == Guid.Empty) return Results.Unauthorized();
            var result = await sender.SendAsync(new GetMyPlayerStateQuery(gameId, sub), ct);
            return result.ToHttpResult();
        }).RequireAuthorization();
    }
}
