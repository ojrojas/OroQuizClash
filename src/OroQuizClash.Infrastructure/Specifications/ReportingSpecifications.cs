using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Questions;

namespace OroQuizClash.Infrastructure.Specifications;

// Note: These specifications are for read filtering in-memory via Game aggregate.
// For repository queries, Game-based specs are preferred; Answer/Round/PointTransaction specs are for composable in-memory filtering.

public sealed class ReportingGamesByCategorySpecification : Specification<Game>
{
    public ReportingGamesByCategorySpecification(Guid categoryId, DateTimeOffset? from, DateTimeOffset? to)
    {
        ApplyAsNoTracking();
        Where(g => g.Configuration.CategoryId.Value == categoryId);
        if (from.HasValue) Where(g => g.CreatedAt >= from.Value);
        if (to.HasValue) Where(g => g.CreatedAt <= to.Value);
    }
}

public sealed class QuestionsByCategorySpecification : Specification<Question>
{
    public QuestionsByCategorySpecification(CategoryId categoryId)
    {
        ApplyAsNoTracking();
        Where(q => q.CategoryId.Value == categoryId.Value);
    }
}
