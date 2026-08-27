using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Application.Features.Questions;

public sealed record AnswerOptionInput(string Text, bool IsCorrect, int DisplayOrder);

public sealed record CreateQuestionCommand(
    string Text,
    Guid CategoryId,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    List<AnswerOptionInput> AnswerOptions) : ICommand<Result<CreateQuestionResponse>>;

public sealed record CreateQuestionResponse(
    Guid Id,
    string Text,
    Guid CategoryId,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    string Status,
    IReadOnlyList<AnswerOptionResponse> AnswerOptions,
    string RowVersion,
    DateTimeOffset CreatedAt);

public sealed record AnswerOptionResponse(Guid Id, string Text, bool IsCorrect, int DisplayOrder);

public sealed class CreateQuestionValidator : IValidator<CreateQuestionCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(CreateQuestionCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length < 3 || request.Text.Trim().Length > 500)
            failures.Add(new ValidationFailure(nameof(request.Text), "Text must be 3-500 characters."));

        if (request.CategoryId == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.CategoryId), "CategoryId is required."));

        if (request.Difficulty < 1 || request.Difficulty > 5)
            failures.Add(new ValidationFailure(nameof(request.Difficulty), "Difficulty must be 1-5."));

        if (string.IsNullOrWhiteSpace(request.AcademicLevel) || request.AcademicLevel.Trim().Length < 2 || request.AcademicLevel.Trim().Length > 100)
            failures.Add(new ValidationFailure(nameof(request.AcademicLevel), "AcademicLevel must be 2-100 characters."));

        if (request.AgeMin < 0 || request.AgeMin > 120)
            failures.Add(new ValidationFailure(nameof(request.AgeMin), "AgeMin must be 0-120."));
        if (request.AgeMax < 0 || request.AgeMax > 120)
            failures.Add(new ValidationFailure(nameof(request.AgeMax), "AgeMax must be 0-120."));
        if (request.AgeMin > request.AgeMax)
            failures.Add(new ValidationFailure(nameof(request.AgeMin), "AgeMin must be <= AgeMax."));

        if (request.AnswerOptions is null || request.AnswerOptions.Count != 4)
            failures.Add(new ValidationFailure(nameof(request.AnswerOptions), "Exactly 4 answer options required (QST-001)."));
        else
        {
            foreach (var opt in request.AnswerOptions)
            {
                if (string.IsNullOrWhiteSpace(opt.Text) || opt.Text.Trim().Length < 1 || opt.Text.Trim().Length > 500)
                    failures.Add(new ValidationFailure(nameof(request.AnswerOptions), $"Each option Text must be 1-500. Invalid: '{opt.Text}'."));
                if (opt.DisplayOrder < 0 || opt.DisplayOrder > 3)
                    failures.Add(new ValidationFailure(nameof(request.AnswerOptions), "DisplayOrder must be 0-3."));
            }

            var correctCount = request.AnswerOptions.Count(o => o.IsCorrect);
            if (correctCount != 1)
                failures.Add(new ValidationFailure(nameof(request.AnswerOptions), $"Exactly 1 correct answer required (QST-002), found {correctCount}."));

            var texts = request.AnswerOptions.Select(o => o.Text.Trim().ToLowerInvariant()).ToList();
            if (texts.Distinct().Count() != texts.Count)
                failures.Add(new ValidationFailure(nameof(request.AnswerOptions), "Answer option texts must be unique within the question."));
        }

        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class CreateQuestionHandler(
    IRepository<Question, QuestionId> repository,
    ICategoryExistenceChecker categoryChecker,
    IUnitOfWork unitOfWork) : ICommandHandler<CreateQuestionCommand, Result<CreateQuestionResponse>>
{
    public async Task<Result<CreateQuestionResponse>> HandleAsync(CreateQuestionCommand command, CancellationToken ct)
    {
        var categoryId = new CategoryId(command.CategoryId);

        var exists = await categoryChecker.ExistsAsync(categoryId, ct);
        if (!exists)
            return Result.Failure<CreateQuestionResponse>(QuestionErrors.CategoryNotFound(command.CategoryId));

        DifficultyLevel difficulty;
        try { difficulty = DifficultyLevel.FromId(command.Difficulty); }
        catch { return Result.Failure<CreateQuestionResponse>(QuestionErrors.QuestionMustHaveDifficulty); }

        AcademicLevel academicLevel;
        try { academicLevel = new AcademicLevel(command.AcademicLevel); }
        catch (Exception ex) { return Result.Failure<CreateQuestionResponse>(QuestionErrors.InvalidAcademicLevel(ex.Message)); }

        AgeRange ageRange;
        try { ageRange = new AgeRange(command.AgeMin, command.AgeMax); }
        catch (Exception ex) { return Result.Failure<CreateQuestionResponse>(QuestionErrors.InvalidAgeRange(ex.Message)); }

        var options = command.AnswerOptions.Select(o => (o.Text, o.IsCorrect, o.DisplayOrder)).ToList();

        var result = Question.Create(
            command.Text,
            categoryId,
            difficulty,
            academicLevel,
            ageRange,
            options,
            Guid.NewGuid(),
            _ => exists);

        if (result.IsFailure)
            return Result.Failure<CreateQuestionResponse>(result.Error);

        var question = result.Value;
        await repository.AddAsync(question, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var response = Map(question);
        return Result.Success(response);
    }

    private static CreateQuestionResponse Map(Question q) => new(
        q.Id.Value,
        q.Text,
        q.CategoryId.Value,
        q.Difficulty.Id,
        q.AcademicLevel.Value,
        q.AgeRange.Min,
        q.AgeRange.Max,
        q.Status.Name,
        q.AnswerOptions.Select(a => new AnswerOptionResponse(a.Id.Value, a.Text, a.IsCorrect, a.DisplayOrder)).ToList(),
        q.RowVersion != null && q.RowVersion.Length > 0 ? Convert.ToBase64String(q.RowVersion) : string.Empty,
        q.CreatedAt);
}

public sealed class CreateQuestionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/questions", async (CreateQuestionCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(command, ct);
            return result.ToCreatedResult(r => $"/api/questions/{r.Id}");
        }).RequireAuthorization("AdminOrGameManager");
    }
}
