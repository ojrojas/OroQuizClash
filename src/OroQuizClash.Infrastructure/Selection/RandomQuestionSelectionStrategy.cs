using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Domain.Questions.ValueObjects;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Infrastructure.Selection;

public sealed class RandomQuestionSelectionStrategy(IRepository<Question, QuestionId> repository) : IQuestionSelectionStrategy
{
    public async Task<Result<IReadOnlyList<Question>>> SelectAsync(QuestionSelectionCriteria criteria, CancellationToken cancellationToken = default)
    {
        var spec = new QuestionSelectionSpecification(criteria);
        var all = await repository.ListAsync(spec, cancellationToken);

        if (all.Count == 0)
            return Result.Failure<IReadOnlyList<Question>>(QuestionErrors.NoAvailableQuestion);

        // Randomize in-memory (small set after filtering) - for large sets use DB random
        var randomized = all.OrderBy(_ => Guid.NewGuid()).Take(criteria.Take).ToList();

        if (randomized.Count == 0)
            return Result.Failure<IReadOnlyList<Question>>(QuestionErrors.NoAvailableQuestion);

        return Result.Success<IReadOnlyList<Question>>(randomized);
    }
}
