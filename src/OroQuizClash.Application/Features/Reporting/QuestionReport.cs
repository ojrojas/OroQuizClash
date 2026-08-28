using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Reporting;

public sealed record GetQuestionReportQuery(
    Guid QuestionId,
    Guid? GameId = null,
    Guid? CategoryId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<Result<QuestionReportResponse>>;

public sealed record QuestionReportResponse(
    Guid QuestionId,
    Guid CategoryId,
    string CategoryName,
    string Difficulty,
    int TimesPresented,
    int CorrectAnswers,
    int IncorrectAnswers,
    double? Accuracy,
    double? AverageResponseTime);

public sealed class GetQuestionReportValidator : IValidator<GetQuestionReportQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(GetQuestionReportQuery request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.QuestionId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.QuestionId), "QuestionId required."));
        if (request.From.HasValue && request.To.HasValue && request.From.Value > request.To.Value)
            failures.Add(new ValidationFailure(nameof(request.From), "from must be <= to"));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class GetQuestionReportHandler(
    IRepository<Question, QuestionId> questionRepository,
    IRepository<Game, GameId> gameRepository) : IQueryHandler<GetQuestionReportQuery, Result<QuestionReportResponse>>
{
    public async Task<Result<QuestionReportResponse>> HandleAsync(GetQuestionReportQuery query, CancellationToken ct)
    {
        var question = await questionRepository.FirstOrDefaultAsync(new QuestionByIdSpecification(new QuestionId(query.QuestionId)), ct);
        if (question is null) return Result.Failure<QuestionReportResponse>(Error.NotFound("Question.NotFound", "Question not found."));

        // TimesPresented = count of GameRounds where QuestionId
        var games = await LoadGamesForQuestion(query, ct);
        var timesPresented = games.Sum(g => g.Rounds.Count(r => r.QuestionId.Value == query.QuestionId));
        if (query.GameId.HasValue) timesPresented = games.Sum(g => g.Rounds.Count(r => r.QuestionId.Value == query.QuestionId && g.Id.Value == query.GameId.Value));

        var correct = 0;
        var incorrect = 0;
        var elapsedTimes = new List<int>();

        foreach (var game in games)
        {
            var answers = game.Answers.Where(a => a.QuestionId.Value == query.QuestionId && a.Status.Name == "EVALUATED").ToList();
            if (query.GameId.HasValue) answers = answers.Where(a => a.GameId.Value == query.GameId.Value).ToList();
            if (query.From.HasValue) answers = answers.Where(a => a.CreatedAt >= query.From.Value).ToList();
            if (query.To.HasValue) answers = answers.Where(a => a.CreatedAt <= query.To.Value).ToList();
            correct += answers.Count(a => a.Correct == true);
            incorrect += answers.Count(a => a.Correct == false);
            elapsedTimes.AddRange(answers.Where(a => a.ElapsedTime >= 0).Select(a => a.ElapsedTime));
        }

        var accuracy = (correct + incorrect) == 0 ? (double?)null : Math.Round((double)correct / (correct + incorrect) * 100, 2);
        var avgTime = elapsedTimes.Count == 0 ? (double?)null : Math.Round(elapsedTimes.Average(), 2);

        // If category filter, ensure question's category matches
        if (query.CategoryId.HasValue && question.CategoryId.Value != query.CategoryId.Value)
        {
            return Result.Success(new QuestionReportResponse(query.QuestionId, question.CategoryId.Value, question.CategoryId.Value.ToString(), question.Difficulty.ToString(), 0, 0, 0, null, null));
        }

        return Result.Success(new QuestionReportResponse(
            question.Id.Value,
            question.CategoryId.Value,
            question.CategoryId.Value.ToString(),
            question.Difficulty.ToString(),
            timesPresented,
            correct,
            incorrect,
            accuracy,
            avgTime));
    }

    private async Task<IReadOnlyList<Game>> LoadGamesForQuestion(GetQuestionReportQuery query, CancellationToken ct)
    {
        if (query.GameId.HasValue)
        {
            var spec = new GameByIdWithAnswersSpecification(new GameId(query.GameId.Value));
            var g = await gameRepository.FirstOrDefaultAsync(spec, ct);
            return g is not null ? [g] : [];
        }
        if (query.CategoryId.HasValue)
        {
            var spec = new ReportingGamesByCategorySpecification(query.CategoryId.Value, query.From, query.To);
            return await gameRepository.ListAsync(spec, ct);
        }
        var periodSpec = new GamesByPeriodSpecification(query.From, query.To);
        return await gameRepository.ListAsync(periodSpec, ct);
    }
}

public sealed class GetQuestionReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/questions/{questionId:guid}", async (
            Guid questionId,
            Guid? gameId,
            Guid? categoryId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetQuestionReportQuery(questionId, gameId, categoryId, from, to), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("Report.Read");
    }
}
