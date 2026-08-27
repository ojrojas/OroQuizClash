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

public sealed record UpdateQuestionCommand(
    Guid Id,
    string Text,
    Guid CategoryId,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    List<AnswerOptionInput> AnswerOptions) : ICommand<Result<CreateQuestionResponse>>;

public sealed class UpdateQuestionValidator : IValidator<UpdateQuestionCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(UpdateQuestionCommand request, CancellationToken ct)
    {
        var failures = new List<ValidationFailure>();

        if (request.Id == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.Id), "Id is required."));

        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Trim().Length < 3 || request.Text.Trim().Length > 500)
            failures.Add(new ValidationFailure(nameof(request.Text), "Text must be 3-500 characters."));

        if (request.CategoryId == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.CategoryId), "CategoryId is required."));

        if (request.Difficulty < 1 || request.Difficulty > 5)
            failures.Add(new ValidationFailure(nameof(request.Difficulty), "Difficulty must be 1-5."));

        if (string.IsNullOrWhiteSpace(request.AcademicLevel) || request.AcademicLevel.Trim().Length < 2 || request.AcademicLevel.Trim().Length > 100)
            failures.Add(new ValidationFailure(nameof(request.AcademicLevel), "AcademicLevel must be 2-100."));

        if (request.AgeMin < 0 || request.AgeMin > 120)
            failures.Add(new ValidationFailure(nameof(request.AgeMin), "AgeMin 0-120."));
        if (request.AgeMax < 0 || request.AgeMax > 120)
            failures.Add(new ValidationFailure(nameof(request.AgeMax), "AgeMax 0-120."));
        if (request.AgeMin > request.AgeMax)
            failures.Add(new ValidationFailure(nameof(request.AgeMin), "AgeMin <= AgeMax."));

        if (request.AnswerOptions is null || request.AnswerOptions.Count != 4)
            failures.Add(new ValidationFailure(nameof(request.AnswerOptions), "Exactly 4 options required."));
        else
        {
            foreach (var opt in request.AnswerOptions)
            {
                if (string.IsNullOrWhiteSpace(opt.Text) || opt.Text.Trim().Length < 1 || opt.Text.Trim().Length > 500)
                    failures.Add(new ValidationFailure(nameof(request.AnswerOptions), $"Each option 1-500. Invalid: '{opt.Text}'."));
            }
            var correct = request.AnswerOptions.Count(o => o.IsCorrect);
            if (correct != 1)
                failures.Add(new ValidationFailure(nameof(request.AnswerOptions), $"Exactly 1 correct required, found {correct}."));
            var texts = request.AnswerOptions.Select(o => o.Text.Trim().ToLowerInvariant()).ToList();
            if (texts.Distinct().Count() != texts.Count)
                failures.Add(new ValidationFailure(nameof(request.AnswerOptions), "Option texts must be unique."));
        }

        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class UpdateQuestionHandler(
    IRepository<Question, QuestionId> repository,
    ICategoryExistenceChecker categoryChecker,
    IUnitOfWork unitOfWork) : ICommandHandler<UpdateQuestionCommand, Result<CreateQuestionResponse>>
{
    public async Task<Result<CreateQuestionResponse>> HandleAsync(UpdateQuestionCommand command, CancellationToken ct)
    {
        var question = await repository.GetByIdAsync(new QuestionId(command.Id), ct);
        if (question is null)
            return Result.Failure<CreateQuestionResponse>(QuestionErrors.QuestionNotFound(command.Id));

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

        var result = question.Update(command.Text, categoryId, difficulty, academicLevel, ageRange, options, _ => exists);
        if (result.IsFailure)
            return Result.Failure<CreateQuestionResponse>(result.Error);

        repository.Update(question);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException)
        {
            return Result.Failure<CreateQuestionResponse>(QuestionErrors.ConcurrencyConflict);
        }

        var response = new CreateQuestionResponse(
            question.Id.Value,
            question.Text,
            question.CategoryId.Value,
            question.Difficulty.Id,
            question.AcademicLevel.Value,
            question.AgeRange.Min,
            question.AgeRange.Max,
            question.Status.Name,
            question.AnswerOptions.Select(a => new AnswerOptionResponse(a.Id.Value, a.Text, a.IsCorrect, a.DisplayOrder)).ToList(),
            question.RowVersion != null && question.RowVersion.Length > 0 ? Convert.ToBase64String(question.RowVersion) : string.Empty,
            question.CreatedAt);

        return Result.Success(response);
    }
}

public sealed class UpdateQuestionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/questions/{id:guid}", async (Guid id, UpdateQuestionCommand body, ISender sender, CancellationToken ct) =>
        {
            var command = body with { Id = id };
            var result = await sender.SendAsync(command, ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}
