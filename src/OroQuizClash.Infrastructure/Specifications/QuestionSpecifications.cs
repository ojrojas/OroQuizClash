using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class ValidQuestionSpecification : Specification<Question>
{
    public ValidQuestionSpecification(CategoryId categoryId)
    {
        ApplyAsNoTracking();
        Where(q => q.Status == QuestionStatus.Published);
        Where(q => q.CategoryId == categoryId);
        Where(q => q.AnswerOptions.Count == 4);
        // Exactly one correct - cannot translate directly to SQL via Count, use in-memory predicate for IsSatisfiedBy
        // For DB translation, we use subquery via Any - EF will translate Count check via SQL
        Where(q => q.AnswerOptions.Count(a => a.IsCorrect) == 1);
    }
}

public sealed class QuestionFilterSpecification : Specification<Question>
{
    public QuestionFilterSpecification(
        CategoryId? categoryId = null,
        int? difficulty = null,
        string? academicLevel = null,
        int? ageMin = null,
        int? ageMax = null,
        string? status = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        bool paginate = true)
    {
        ApplyAsNoTracking();

        if (categoryId is not null)
            Where(q => q.CategoryId == categoryId);

        if (difficulty.HasValue)
            Where(q => q.Difficulty.Id == difficulty.Value);

        if (!string.IsNullOrWhiteSpace(academicLevel))
        {
            var trimmed = academicLevel.Trim();
            Where(q => q.AcademicLevel.Value == trimmed);
        }

        if (ageMin.HasValue)
            Where(q => q.AgeRange.Max >= ageMin.Value);

        if (ageMax.HasValue)
            Where(q => q.AgeRange.Min <= ageMax.Value);

        if (!string.IsNullOrWhiteSpace(status))
        {
            try
            {
                var st = QuestionStatus.FromName(status.Trim());
                Where(q => q.Status == st);
            }
            catch (ArgumentOutOfRangeException)
            {
                Where(q => false);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            Where(q => q.Text.Contains(term));
        }

        AddInclude(q => q.AnswerOptions);

        ApplyOrderByDescending(q => (object)q.CreatedAt);

        if (paginate)
        {
            var safePage = page < 1 ? 1 : page;
            var safeSize = pageSize < 1 ? 20 : pageSize > 100 ? 100 : pageSize;
            var skip = (safePage - 1) * safeSize;
            ApplyPaging(skip, safeSize);
        }
    }

    public static QuestionFilterSpecification ForCount(
        CategoryId? categoryId = null,
        int? difficulty = null,
        string? academicLevel = null,
        string? status = null) =>
        new(categoryId, difficulty, academicLevel, null, null, status, null, 1, 20, paginate: false);
}

public sealed class QuestionByIdSpecification : Specification<Question>
{
    public QuestionByIdSpecification(QuestionId id)
    {
        Where(q => q.Id == id);
        AddInclude(q => q.AnswerOptions);
        ApplyAsNoTracking();
    }
}

public sealed class QuestionSelectionSpecification : Specification<Question>
{
    public QuestionSelectionSpecification(QuestionSelectionCriteria criteria)
    {
        ApplyAsNoTracking();
        AddInclude(q => q.AnswerOptions);

        Where(q => q.Status == QuestionStatus.Published);
        Where(q => q.AnswerOptions.Count == 4);
        Where(q => q.AnswerOptions.Count(a => a.IsCorrect) == 1);

        if (criteria.CategoryId is not null)
            Where(q => q.CategoryId == criteria.CategoryId);

        if (criteria.Difficulty is not null)
            Where(q => q.Difficulty.Id == criteria.Difficulty.Id);

        if (!string.IsNullOrWhiteSpace(criteria.AcademicLevel))
        {
            var lvl = criteria.AcademicLevel.Trim();
            Where(q => q.AcademicLevel.Value == lvl);
        }

        if (criteria.AgeRange is not null)
        {
            Where(q => q.AgeRange.Max >= criteria.AgeRange.Min && q.AgeRange.Min <= criteria.AgeRange.Max);
        }

        if (criteria.PreviousQuestionIds.Count > 0)
        {
            Where(q => !criteria.PreviousQuestionIds.Contains(q.Id));
        }
    }
}
