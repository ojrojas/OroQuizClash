using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using OroQuizClash.Domain.Questions;

namespace OroQuizClash.Application.Features.Questions;

public sealed record PublishQuestionCommand(Guid Id) : ICommand<Result<CreateQuestionResponse>>;

public sealed class PublishQuestionHandler(
    IRepository<Question, QuestionId> repository,
    IUnitOfWork unitOfWork) : ICommandHandler<PublishQuestionCommand, Result<CreateQuestionResponse>>
{
    public async Task<Result<CreateQuestionResponse>> HandleAsync(PublishQuestionCommand command, CancellationToken ct)
    {
        var question = await repository.FirstOrDefaultAsync(
            new OroQuizClash.Infrastructure.Specifications.QuestionByIdSpecification(new QuestionId(command.Id)), ct);
        if (question is null)
            return Result.Failure<CreateQuestionResponse>(QuestionErrors.QuestionNotFound(command.Id));

        var result = question.Publish();
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

public sealed class PublishQuestionEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/questions/{id:guid}/publish", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.SendAsync(new PublishQuestionCommand(id), ct);
            return result.ToHttpResult();
        }).RequireAuthorization("AdminOrGameManager");
    }
}
