using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Application.Features.Questions.Services;

public interface IQuestionSelectionStrategy
{
    Task<Result<IReadOnlyList<Question>>> SelectAsync(QuestionSelectionCriteria criteria, CancellationToken cancellationToken = default);
}
