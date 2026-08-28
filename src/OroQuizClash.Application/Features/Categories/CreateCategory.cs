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

using OroQuizClash.Application.Authorization;

namespace OroQuizClash.Application.Features.Categories;

[RequiresPermission("Category.Write")]
public sealed record CreateCategoryCommand(
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int DifficultyLevel,
    List<string>? Tags,
    bool RequiresModeration = false) : ICommand<Result<CreateCategoryResponse>>;

public sealed record CreateCategoryResponse(
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

public sealed class CreateCategoryValidator : IValidator<CreateCategoryCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(CreateCategoryCommand request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

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

public sealed class CreateCategoryHandler(IRepository<Category, CategoryId> repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCategoryCommand, Result<CreateCategoryResponse>>
{
    public async Task<Result<CreateCategoryResponse>> HandleAsync(CreateCategoryCommand command, CancellationToken ct)
    {
        // Build VOs with exception handling -> Result.Failure
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
            return Result.Failure<CreateCategoryResponse>(CategoryErrors.InvalidCategoryConfiguration(ex.Message));
        }

        try
        {
            academicLevel = new AcademicLevel(command.AcademicLevel);
        }
        catch (Exception ex)
        {
            return Result.Failure<CreateCategoryResponse>(CategoryErrors.InvalidCategoryConfiguration(ex.Message));
        }

        try
        {
            ageRange = new AgeRange(command.AgeMin, command.AgeMax);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Result.Failure<CreateCategoryResponse>(CategoryErrors.InvalidAgeRangeDetail(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return Result.Failure<CreateCategoryResponse>(CategoryErrors.InvalidAgeRangeDetail(ex.Message));
        }

        try
        {
            difficultyLevel = new DifficultyLevel(command.DifficultyLevel);
        }
        catch (Exception ex)
        {
            return Result.Failure<CreateCategoryResponse>(CategoryErrors.InvalidDifficultyDetail(ex.Message));
        }

        try
        {
            categoryTags = command.Tags == null ? CategoryTags.Empty : new CategoryTags(command.Tags);
        }
        catch (Exception ex)
        {
            return Result.Failure<CreateCategoryResponse>(CategoryErrors.InvalidTagsDetail(ex.Message));
        }

        try
        {
            publishConfiguration = new PublishConfiguration(command.RequiresModeration);
        }
        catch (Exception ex)
        {
            return Result.Failure<CreateCategoryResponse>(CategoryErrors.InvalidCategoryConfiguration(ex.Message));
        }

        var result = Category.Create(
            command.Name,
            command.Description ?? string.Empty,
            knowledgeArea,
            academicLevel,
            ageRange,
            difficultyLevel,
            categoryTags,
            publishConfiguration,
            Guid.NewGuid());

        if (result.IsFailure)
            return Result.Failure<CreateCategoryResponse>(result.Error);

        var category = result.Value;
        await repository.AddAsync(category, ct);
        await unitOfWork.SaveChangesAsync(ct);

        var response = new CreateCategoryResponse(
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

public sealed class CreateCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/categories", async (CreateCategoryCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(command, ct);
            return result.ToCreatedResult(r => $"/api/categories/{r.Id}");
        }).RequireAuthorization("AdminOrGameManager");
    }
}