using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Features.Reporting;

public sealed record GetCategoryReportQuery(
    Guid CategoryId,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IQuery<Result<CategoryReportResponse>>;

public sealed record CategoryReportResponse(
    Guid CategoryId,
    string CategoryName,
    int Questions,
    int Games,
    int Players,
    double? AverageScore,
    double? AverageAccuracy);

public sealed class GetCategoryReportValidator : IValidator<GetCategoryReportQuery>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(GetCategoryReportQuery request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();
        if (request.CategoryId == Guid.Empty) failures.Add(new ValidationFailure(nameof(request.CategoryId), "CategoryId required."));
        if (request.From.HasValue && request.To.HasValue && request.From.Value > request.To.Value)
            failures.Add(new ValidationFailure(nameof(request.From), "from must be <= to"));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class GetCategoryReportHandler(
    IRepository<Category, CategoryId> categoryRepository,
    IRepository<Question, QuestionId> questionRepository,
    IRepository<Game, GameId> gameRepository) : IQueryHandler<GetCategoryReportQuery, Result<CategoryReportResponse>>
{
    public async Task<Result<CategoryReportResponse>> HandleAsync(GetCategoryReportQuery query, CancellationToken ct)
    {
        var category = await categoryRepository.GetByIdAsync(new CategoryId(query.CategoryId), ct);
        if (category is null) return Result.Failure<CategoryReportResponse>(Error.NotFound("Category.NotFound", "Category not found."));

        var questionSpec = new QuestionsByCategorySpecification(new CategoryId(query.CategoryId));
        var questions = await questionRepository.ListAsync(questionSpec, ct);
        var questionsCount = questions.Count;

        var gamesSpec = new ReportingGamesByCategorySpecification(query.CategoryId, query.From, query.To);
        var games = await gameRepository.ListAsync(gamesSpec, ct);
        var gamesCount = games.Count;
        var players = games.SelectMany(g => g.Players).Select(p => p.UserId).Distinct().Count();

        double? avgScore = null;
        double? avgAccuracy = null;
        if (gamesCount > 0)
        {
            var scores = new List<int>();
            var accuracies = new List<double>();
            foreach (var game in games)
            {
                var lb = Features.Games.LeaderboardBuilder.Build(game);
                foreach (var entry in lb)
                {
                    scores.Add(entry.Points);
                    // Accuracy per player in this game: correct / answered
                    var playerAnswers = game.Answers.Where(a => a.PlayerId == entry.PlayerId && a.Status.Name == "EVALUATED").ToList();
                    if (playerAnswers.Count > 0)
                    {
                        var correct = playerAnswers.Count(a => a.Correct == true);
                        accuracies.Add((double)correct / playerAnswers.Count * 100);
                    }
                }
            }
            if (scores.Count > 0) avgScore = Math.Round(scores.Average(), 2);
            if (accuracies.Count > 0) avgAccuracy = Math.Round(accuracies.Average(), 2);
        }

        return Result.Success(new CategoryReportResponse(
            category.Id.Value,
            category.Name,
            questionsCount,
            gamesCount,
            players,
            avgScore,
            avgAccuracy));
    }
}

public sealed class GetCategoryReportEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/reports/categories/{categoryId:guid}", async (
            Guid categoryId,
            DateTimeOffset? from,
            DateTimeOffset? to,
            ISender sender,
            CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new GetCategoryReportQuery(categoryId, from, to), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("Report.Read");
    }
}
