using BuildingBlocks.Kernel.Domain.Specifications;

using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class CategoryFilterSpecification : Specification<Category>
{
    public CategoryFilterSpecification(
        string? knowledgeArea = null,
        string? academicLevel = null,
        int? difficultyLevel = null,
        string? state = null,
        string? tag = null,
        int? ageMin = null,
        int? ageMax = null,
        int page = 1,
        int pageSize = 20,
        bool paginate = true)
    {
        ApplyAsNoTracking();

        if (!string.IsNullOrWhiteSpace(knowledgeArea))
        {
            var trimmed = knowledgeArea.Trim();
            Where(c => c.KnowledgeArea.Value == trimmed);
        }

        if (!string.IsNullOrWhiteSpace(academicLevel))
        {
            var trimmed = academicLevel.Trim();
            Where(c => c.AcademicLevel.Value == trimmed);
        }

        if (difficultyLevel.HasValue)
        {
            var lvl = difficultyLevel.Value;
            Where(c => c.DifficultyLevel.Value == lvl);
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            var trimmed = state.Trim();
            try
            {
                var status = CategoryStatus.FromName(trimmed);
                Where(c => c.Status == status);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Unknown state -> no results
                Where(c => false);
            }
        }

        if (!string.IsNullOrWhiteSpace(tag))
        {
            var normalized = tag.Trim().ToLowerInvariant();
            // Tags are stored as CSV string via value converter; use property directly for in-memory / EF translation
            // The CategoryTags.Value is IReadOnlySet<string> - use Contains
            Where(c => c.Tags.Value.Contains(normalized));
        }

        if (ageMin.HasValue)
        {
            var min = ageMin.Value;
            Where(c => c.AgeRange.Max >= min);
        }

        if (ageMax.HasValue)
        {
            var max = ageMax.Value;
            Where(c => c.AgeRange.Min <= max);
        }

        ApplyOrderBy(c => (object)c.Name);

        if (paginate)
        {
            var safePage = page < 1 ? 1 : page;
            var safeSize = pageSize < 1 ? 20 : pageSize > 100 ? 100 : pageSize;
            var skip = (safePage - 1) * safeSize;
            ApplyPaging(skip, safeSize);
        }
    }

    // Non-paginated overload for counting total
    public static CategoryFilterSpecification ForCount(
        string? knowledgeArea = null,
        string? academicLevel = null,
        int? difficultyLevel = null,
        string? state = null,
        string? tag = null,
        int? ageMin = null,
        int? ageMax = null) =>
        new(knowledgeArea, academicLevel, difficultyLevel, state, tag, ageMin, ageMax, 1, 20, paginate: false);
}

public sealed class CategoryByIdSpecification : Specification<Category>
{
    public CategoryByIdSpecification(CategoryId id)
    {
        Where(c => c.Id == id);
        ApplyAsNoTracking();
    }

    public CategoryByIdSpecification(Guid id) : this(new CategoryId(id))
    {
    }
}