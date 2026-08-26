using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Categories.ValueObjects;

namespace OroQuizClash.Application.Features.Categories;

public sealed record UpdateCategoryCommand(
    Guid Id,
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int DifficultyLevel,
    List<string>? Tags,
    bool RequiresModeration = false) : ICommand<Result<UpdateCategoryResponse>>;

public sealed record UpdateCategoryResponse(
    Guid Id,
    string Name,
    string Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int DifficultyLevel,
    IReadOnlyCollection<string> Tags,
    string Status,
    string RowVersion);

public sealed class UpdateCategoryValidator : IValidator<UpdateCategoryCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(UpdateCategoryCommand request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        if (request.Id == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.Id), "Id is required."));

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length < 3 || request.Name.Trim().Length > 100)
            failures.Add(new ValidationFailure(nameof(request.Name), "Name must be 3-100 characters."));

        if (request.Description != null && request.Description.Length > 500)
            failures.Add(new ValidationFailure(nameof(request.Description), "Description must be 0-500 characters."));

        if (string.IsNullOrWhiteSpace(request.KnowledgeArea) || request.KnowledgeArea.Trim().Length < 2 || request.KnowledgeArea.Trim().Length > 100)
            failures.Add(new ValidationFailure(nameof(request.KnowledgeArea), "KnowledgeArea must be 2-100 characters."));

        if (string.IsNullOrWhiteSpace(request.AcademicLevel) || request.AcademicLevel.Trim().Length < 2 || request.AcademicLevel.Trim().Length > 100)
            failures.Add(new ValidationFailure(nameof(request.AcademicLevel), "AcademicLevel must be 2-100 characters."));

        if (request.AgeMin < 0 || request.AgeMin > 120)
            failures.Add(new ValidationFailure(nameof(request.AgeMin), "AgeMin must be 0-120."));
        if (request.AgeMax < 0 || request.AgeMax > 120)
            failures.Add(new ValidationFailure(nameof(request.AgeMax), "AgeMax must be 0-120."));
        if (request.AgeMin > request.AgeMax)
            failures.Add(new ValidationFailure(nameof(request.AgeMin), "AgeMin must be <= AgeMax."));

        if (request.DifficultyLevel < 1 || request.DifficultyLevel > 5)
            failures.Add(new ValidationFailure(nameof(request.DifficultyLevel), "DifficultyLevel must be 1-5."));

        if (request.Tags != null && request.Tags.Count > 10)
            failures.Add(new ValidationFailure(nameof(request.Tags), "Tags must be <=10 items."));

        if (request.Tags != null)
        {
            foreach (var tag in request.Tags)
            {
                if (string.IsNullOrWhiteSpace(tag) || tag.Trim().Length < 2 || tag.Trim().Length > 30)
                    failures.Add(new ValidationFailure(nameof(request.Tags), $"Each tag must be 2-30 characters. Invalid: '{tag}'."));
            }
        }

        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class UpdateCategoryHandler(IRepository<Category, CategoryId> repository, IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCategoryCommand, Result<UpdateCategoryResponse>>
{
    public async Task<Result<UpdateCategoryResponse>> HandleAsync(UpdateCategoryCommand command, CancellationToken ct)
    {
        var categoryId = new CategoryId(command.Id);
        var category = await repository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result.Failure<UpdateCategoryResponse>(CategoryErrors.CategoryNotFound(command.Id));

        KnowledgeArea knowledgeArea;
        AcademicLevel academicLevel;
        AgeRange ageRange;
        DifficultyLevel difficultyLevel;
        CategoryTags categoryTags;
        PublishConfiguration publishConfiguration;

        try
        {
            knowledgeArea = new KnowledgeArea(command.KnowledgeArea);
        }
        catch (Exception ex)
        {
            return Result.Failure<UpdateCategoryResponse>(CategoryErrors.InvalidCategoryConfiguration(ex.Message));
        }

        try
        {
            academicLevel = new AcademicLevel(command.AcademicLevel);
        }
        catch (Exception ex)
        {
            return Result.Failure<UpdateCategoryResponse>(CategoryErrors.InvalidCategoryConfiguration(ex.Message));
        }

        try
        {
            ageRange = new AgeRange(command.AgeMin, command.AgeMax);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result.Failure<UpdateCategoryResponse>(CategoryErrors.InvalidAgeRangeDetail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<UpdateCategoryResponse>(CategoryErrors.InvalidAgeRangeDetail(ex.Message));
        }

        try
        {
            difficultyLevel = new DifficultyLevel(command.DifficultyLevel);
        }
        catch (Exception ex)
        {
            return Result.Failure<UpdateCategoryResponse>(CategoryErrors.InvalidDifficultyDetail(ex.Message));
        }

        try
        {
            categoryTags = command.Tags == null ? CategoryTags.Empty : new CategoryTags(command.Tags);
        }
        catch (Exception ex)
        {
            return Result.Failure<UpdateCategoryResponse>(CategoryErrors.InvalidTagsDetail(ex.Message));
        }

        try
        {
            publishConfiguration = new PublishConfiguration(command.RequiresModeration);
        }
        catch (Exception ex)
        {
            return Result.Failure<UpdateCategoryResponse>(CategoryErrors.InvalidCategoryConfiguration(ex.Message));
        }

        var updateResult = category.Update(
            command.Name,
            command.Description ?? string.Empty,
            knowledgeArea,
            academicLevel,
            ageRange,
            difficultyLevel,
            categoryTags,
            publishConfiguration);

        if (updateResult.IsFailure)
            return Result.Failure<UpdateCategoryResponse>(updateResult.Error);

        repository.Update(category);
        await unitOfWork.SaveChangesAsync(ct);

        var response = new UpdateCategoryResponse(
            category.Id.Value,
            category.Name,
            category.Description,
            category.KnowledgeArea.Value,
            category.AcademicLevel.Value,
            category.AgeRange.Min,
            category.AgeRange.Max,
            category.DifficultyLevel.Value,
            category.Tags.Tags.ToList(),
            category.Status.Name,
            category.RowVersion != null && category.RowVersion.Length > 0 ? Convert.ToBase64String(category.RowVersion) : string.Empty);

        return Result.Success(response);
    }
}

public sealed class UpdateCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPut("/api/categories/{id:guid}", async (Guid id, UpdateCategoryCommand command, ISender sender, CancellationToken ct) =>
        {
            var cmd = command with { Id = id };
            var result = await sender.SendAsync(cmd, ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}