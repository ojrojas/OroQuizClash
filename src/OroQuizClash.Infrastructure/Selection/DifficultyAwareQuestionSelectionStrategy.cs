using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Services;
using OroQuizClash.Domain.Questions.ValueObjects;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Infrastructure.Selection;

public sealed class DifficultyAwareQuestionSelectionStrategy(IRepository<Question, QuestionId> repository) : IQuestionSelectionStrategy
{
    public async Task<Result<IReadOnlyList<Question>>> SelectAsync(QuestionSelectionCriteria criteria, CancellationToken cancellationToken = default)
    {
        // Primary: exact difficulty
        var primarySpec = new QuestionSelectionSpecification(criteria);
        var primary = await repository.ListAsync(primarySpec, cancellationToken);

        if (primary.Count > 0)
        {
            var pick = primary.OrderBy(_ => Guid.NewGuid()).Take(criteria.Take).ToList();
            return Result.Success<IReadOnlyList<Question>>(pick);
        }

        // Fallback: difficulty ±1 if exact not found and difficulty specified
        if (criteria.Difficulty is not null)
        {
            var difficulties = new List<int> { criteria.Difficulty.Id };
            if (criteria.Difficulty.Id > 1) difficulties.Add(criteria.Difficulty.Id - 1);
            if (criteria.Difficulty.Id < 5) difficulties.Add(criteria.Difficulty.Id + 1);

            foreach (var diffId in difficulties.Skip(1))
            {
                var fallbackCriteria = new QuestionSelectionCriteria(
                    criteria.CategoryId,
                    DifficultyLevel.FromId(diffId),
                    criteria.AcademicLevel,
                    criteria.AgeRange,
                    criteria.PreviousQuestionIds,
                    criteria.GameId,
                    criteria.RoundNumber,
                    criteria.RoundId,
                    criteria.Take);

                var fallbackSpec = new QuestionSelectionSpecification(fallbackCriteria);
                var fallback = await repository.ListAsync(fallbackSpec, cancellationToken);
                if (fallback.Count > 0)
                {
                    var pick = fallback.OrderBy(_ => Guid.NewGuid()).Take(criteria.Take).ToList();
                    return Result.Success<IReadOnlyList<Question>>(pick);
                }
            }
        }

        return Result.Failure<IReadOnlyList<Question>>(QuestionErrors.NoAvailableQuestion);
    }
}
