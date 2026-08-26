using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Categories.Events;
using OroQuizClash.Domain.Categories.ValueObjects;

namespace OroQuizClash.Domain.Categories;

public sealed class Category : AggregateRoot<CategoryId>
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public KnowledgeArea KnowledgeArea { get; private set; } = null!;
    public AcademicLevel AcademicLevel { get; private set; } = null!;
    public AgeRange AgeRange { get; private set; } = null!;
    public DifficultyLevel DifficultyLevel { get; private set; } = null!;
    public CategoryTags Tags { get; private set; } = CategoryTags.Empty;
    public PublishConfiguration PublishConfiguration { get; private set; } = null!;
    public CategoryStatus Status { get; private set; } = CategoryStatus.Draft;
    public byte[] RowVersion { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private Category() { }

    private Category(
        CategoryId id,
        string name,
        string description,
        KnowledgeArea knowledgeArea,
        AcademicLevel academicLevel,
        AgeRange ageRange,
        DifficultyLevel difficultyLevel,
        CategoryTags tags,
        PublishConfiguration publishConfiguration,
        CategoryStatus status,
        Guid createdBy)
        : base(id)
    {
        Name = name;
        Description = description;
        KnowledgeArea = knowledgeArea;
        AcademicLevel = academicLevel;
        AgeRange = ageRange;
        DifficultyLevel = difficultyLevel;
        Tags = tags;
        PublishConfiguration = publishConfiguration;
        Status = status;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
    }

    public static Result<Category> Create(
        string name,
        string description,
        KnowledgeArea knowledgeArea,
        AcademicLevel academicLevel,
        AgeRange ageRange,
        DifficultyLevel difficultyLevel,
        CategoryTags tags,
        PublishConfiguration publishConfiguration,
        Guid createdBy)
    {
        var validation = ValidateFields(name, description, knowledgeArea, academicLevel, ageRange, difficultyLevel, tags);
        if (validation.IsFailure) return Result.Failure<Category>(validation.Error);

        var category = new Category(
            new CategoryId(Guid.NewGuid()),
            name.Trim(),
            description?.Trim() ?? string.Empty,
            knowledgeArea,
            academicLevel,
            ageRange,
            difficultyLevel,
            tags ?? CategoryTags.Empty,
            publishConfiguration ?? new PublishConfiguration(false),
            CategoryStatus.Draft,
            createdBy);

        category.RaiseDomainEvent(new CategoryCreatedDomainEvent(category.Id.Value));
        return Result.Success(category);
    }

    public Result Update(
        string name,
        string description,
        KnowledgeArea knowledgeArea,
        AcademicLevel academicLevel,
        AgeRange ageRange,
        DifficultyLevel difficultyLevel,
        CategoryTags tags,
        PublishConfiguration publishConfiguration)
    {
        if (Status != CategoryStatus.Draft && Status != CategoryStatus.Inactive)
            return Result.Failure(CategoryErrors.InvalidCategoryState("Update only allowed in DRAFT or INACTIVE."));

        var validation = ValidateFields(name, description, knowledgeArea, academicLevel, ageRange, difficultyLevel, tags);
        if (validation.IsFailure) return validation;

        Name = name.Trim();
        Description = description?.Trim() ?? string.Empty;
        KnowledgeArea = knowledgeArea;
        AcademicLevel = academicLevel;
        AgeRange = ageRange;
        DifficultyLevel = difficultyLevel;
        Tags = tags ?? CategoryTags.Empty;
        PublishConfiguration = publishConfiguration ?? new PublishConfiguration(false);

        RaiseDomainEvent(new CategoryUpdatedDomainEvent(Id.Value));
        return Result.Success();
    }

    public Result Activate()
    {
        if (Status != CategoryStatus.Draft && Status != CategoryStatus.Inactive)
            return Result.Failure(CategoryErrors.InvalidCategoryState("Activate only from DRAFT or INACTIVE."));

        Status = CategoryStatus.Active;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (Status != CategoryStatus.Active)
            return Result.Failure(CategoryErrors.InvalidCategoryState("Deactivate only from ACTIVE."));

        Status = CategoryStatus.Inactive;
        return Result.Success();
    }

    public async Task<Result> PublishAsync(IQuestionCounter counter, CancellationToken ct = default)
    {
        if (Status != CategoryStatus.Draft && Status != CategoryStatus.Inactive && Status != CategoryStatus.Active)
            return Result.Failure(CategoryErrors.InvalidCategoryState("Publish only from DRAFT, INACTIVE or already ACTIVE."));

        if (Status == CategoryStatus.Archived)
            return Result.Failure(CategoryErrors.InvalidCategoryState("Cannot publish ARCHIVED category."));

        var count = await counter.CountValidAsync(Id, ct);
        if (count < 5)
            return Result.Failure(CategoryErrors.CategoryNotPublishable);

        Status = CategoryStatus.Active;
        RaiseDomainEvent(new CategoryPublishedDomainEvent(Id.Value));
        return Result.Success();
    }

    public Result Archive()
    {
        if (Status == CategoryStatus.Archived)
            return Result.Failure(CategoryErrors.InvalidCategoryState("Already ARCHIVED."));

        // DRAFT, ACTIVE, INACTIVE -> ARCHIVED allowed per FR-003
        Status = CategoryStatus.Archived;
        RaiseDomainEvent(new CategoryArchivedDomainEvent(Id.Value));
        return Result.Success();
    }

    private static Result ValidateFields(
        string name,
        string description,
        KnowledgeArea knowledgeArea,
        AcademicLevel academicLevel,
        AgeRange ageRange,
        DifficultyLevel difficultyLevel,
        CategoryTags tags)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length < 3 || name.Trim().Length > 100)
            return Result.Failure(CategoryErrors.InvalidName);

        if (description != null && description.Length > 500)
            return Result.Failure(CategoryErrors.InvalidCategoryConfiguration("Description must be 0-500 characters."));

        if (knowledgeArea == null || knowledgeArea.Value.Length < 2 || knowledgeArea.Value.Length > 100)
            return Result.Failure(CategoryErrors.InvalidCategoryConfiguration("KnowledgeArea must be 2-100 characters."));

        if (academicLevel == null || academicLevel.Value.Length < 2 || academicLevel.Value.Length > 100)
            return Result.Failure(CategoryErrors.InvalidCategoryConfiguration("AcademicLevel must be 2-100 characters."));

        if (ageRange == null)
            return Result.Failure(CategoryErrors.InvalidAgeRange);

        if (difficultyLevel == null || difficultyLevel.Value < 1 || difficultyLevel.Value > 5)
            return Result.Failure(CategoryErrors.InvalidDifficulty);

        if (tags == null)
            return Result.Failure(CategoryErrors.InvalidTags);

        // Tags already validated via VO constructor (max 10, 2-30)
        return Result.Success();
    }
}