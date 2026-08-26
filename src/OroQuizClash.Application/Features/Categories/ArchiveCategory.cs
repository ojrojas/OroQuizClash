using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.CQRS.Validation;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Application.Features.Categories;

public sealed record ArchiveCategoryCommand(Guid Id) : ICommand<Result<ArchiveCategoryResponse>>;

public sealed record ArchiveCategoryResponse(
    Guid Id,
    string Name,
    string Status,
    string RowVersion);

public sealed class ArchiveCategoryValidator : IValidator<ArchiveCategoryCommand>
{
    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(ArchiveCategoryCommand request, CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        if (request.Id == Guid.Empty)
            failures.Add(new ValidationFailure(nameof(request.Id), "Id is required."));
        return Task.FromResult<IReadOnlyCollection<ValidationFailure>>(failures);
    }
}

public sealed class ArchiveCategoryHandler(IRepository<Category, CategoryId> repository, IUnitOfWork unitOfWork)
    : ICommandHandler<ArchiveCategoryCommand, Result<ArchiveCategoryResponse>>
{
    public async Task<Result<ArchiveCategoryResponse>> HandleAsync(ArchiveCategoryCommand command, CancellationToken ct)
    {
        var categoryId = new CategoryId(command.Id);
        var category = await repository.GetByIdAsync(categoryId, ct);
        if (category is null)
            return Result.Failure<ArchiveCategoryResponse>(CategoryErrors.CategoryNotFound(command.Id));

        var result = category.Archive();
        if (result.IsFailure)
            return Result.Failure<ArchiveCategoryResponse>(result.Error);

        repository.Update(category);
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<ArchiveCategoryResponse>(CategoryErrors.ConcurrencyConflict);
        }

        var response = new ArchiveCategoryResponse(
            category.Id.Value,
            category.Name,
            category.Status.Name,
            category.RowVersion != null && category.RowVersion.Length > 0 ? Convert.ToBase64String(category.RowVersion) : string.Empty);

        return Result.Success(response);
    }
}

public sealed class ArchiveCategoryEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/categories/{id:guid}/archive", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new ArchiveCategoryCommand(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}