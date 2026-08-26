using BuildingBlocks.Kernel.Domain.Results;

namespace OroQuizClash.Domain.Categories;

public static class CategoryErrors
{
    public static Error InvalidCategoryConfiguration() =>
        Error.Validation("InvalidCategoryConfiguration", "Category configuration is invalid.");

    public static Error InvalidCategoryConfiguration(string detail) =>
        Error.Validation("InvalidCategoryConfiguration", detail);

    public static Error InvalidName =>
        Error.Validation("InvalidCategoryConfiguration.InvalidName", "Category name must be 3-100 characters and not empty.");

    public static Error InvalidNameDetail(string detail) =>
        Error.Validation("InvalidCategoryConfiguration.InvalidName", detail);

    public static Error InvalidAgeRange =>
        Error.Validation("InvalidCategoryConfiguration.InvalidAgeRange", "Age range invalid: min must be <= max and between 0 and 120.");

    public static Error InvalidAgeRangeDetail(string detail) =>
        Error.Validation("InvalidCategoryConfiguration.InvalidAgeRange", detail);

    public static Error InvalidTags =>
        Error.Validation("InvalidCategoryConfiguration.InvalidTags", "Tags invalid: max 10 tags, each 2-30 characters, lowercased and deduplicated.");

    public static Error InvalidTagsDetail(string detail) =>
        Error.Validation("InvalidCategoryConfiguration.InvalidTags", detail);

    public static Error InvalidDifficulty =>
        Error.Validation("InvalidCategoryConfiguration.InvalidDifficulty", "Difficulty level must be between 1 and 5.");

    public static Error InvalidDifficultyDetail(string detail) =>
        Error.Validation("InvalidCategoryConfiguration.InvalidDifficulty", detail);

    public static Error CategoryNotPublishable =>
        Error.Validation("CategoryNotPublishable", "Category cannot be published: requires at least 5 valid questions.");

    public static Error CategoryNotPublishableDetail(string detail) =>
        Error.Validation("CategoryNotPublishable", detail);

    public static Error CategoryNotReady =>
        Error.Validation("CategoryNotReady", "Category is not ready: requires at least 5 valid questions and published status.");

    public static Error CategoryNotReadyDetail(string detail) =>
        Error.Validation("CategoryNotReady", detail);

    public static Error InvalidCategoryState() =>
        Error.Validation("InvalidCategoryState", "Invalid category state for this operation.");

    public static Error InvalidCategoryState(string detail) =>
        Error.Validation("InvalidCategoryState", detail);

    public static Error CategoryNotFound() =>
        Error.NotFound("CategoryNotFound", "Category not found.");

    public static Error CategoryNotFound(Guid id) =>
        Error.NotFound("CategoryNotFound", $"Category with id '{id}' not found.");

    public static Error ConcurrencyConflict =>
        Error.Conflict("ConcurrencyConflict", "Category was modified concurrently. Reload and retry.");

    public static Error ConcurrencyConflictDetail(string detail) =>
        Error.Conflict("ConcurrencyConflict", detail);
}