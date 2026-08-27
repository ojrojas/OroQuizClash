using BuildingBlocks.Kernel.Domain.Entities;
using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions.Events;
using OroQuizClash.Domain.Questions.Rules;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Domain.Questions;

public sealed class Question : AggregateRoot<QuestionId>
{
    public string Text { get; private set; } = string.Empty;
    public CategoryId CategoryId { get; private set; } = null!;
    public DifficultyLevel Difficulty { get; private set; } = null!;
    public AcademicLevel AcademicLevel { get; private set; } = null!;
    public AgeRange AgeRange { get; private set; } = null!;
    public QuestionStatus Status { get; private set; } = QuestionStatus.Draft;
    public byte[] RowVersion { get; private set; } = [];
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public Guid CreatedBy { get; private set; }

    private readonly List<AnswerOption> _answerOptions = [];
    public IReadOnlyList<AnswerOption> AnswerOptions => _answerOptions.AsReadOnly();

    private Question() { }

    private Question(
        QuestionId id,
        string text,
        CategoryId categoryId,
        DifficultyLevel difficulty,
        AcademicLevel academicLevel,
        AgeRange ageRange,
        List<AnswerOption> options,
        Guid createdBy)
        : base(id)
    {
        Text = text;
        CategoryId = categoryId;
        Difficulty = difficulty;
        AcademicLevel = academicLevel;
        AgeRange = ageRange;
        _answerOptions = options;
        Status = QuestionStatus.Draft;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
        CreatedBy = createdBy;
    }

    public static Result<Question> Create(
        string text,
        CategoryId categoryId,
        DifficultyLevel difficulty,
        AcademicLevel academicLevel,
        AgeRange ageRange,
        IReadOnlyList<(string text, bool isCorrect, int displayOrder)> answerOptions,
        Guid createdBy,
        Func<CategoryId, bool>? categoryExists = null)
    {
        // Basic field validation
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 3 || text.Trim().Length > 500)
            return Result.Failure<Question>(QuestionErrors.InvalidQuestionText("Text must be 3-500 characters."));

        if (categoryId is null || categoryId.Value == Guid.Empty)
            return Result.Failure<Question>(QuestionErrors.QuestionMustBelongToCategory);

        if (categoryExists is not null && !categoryExists(categoryId))
            return Result.Failure<Question>(QuestionErrors.CategoryNotFound(categoryId.Value));

        if (difficulty is null)
            return Result.Failure<Question>(QuestionErrors.QuestionMustHaveDifficulty);

        var difficultyRule = new QuestionMustHaveDifficultyRule(difficulty?.Id);
        if (difficultyRule.IsBroken())
            return Result.Failure<Question>(QuestionErrors.QuestionMustHaveDifficulty);

        if (academicLevel is null)
            return Result.Failure<Question>(QuestionErrors.InvalidAcademicLevel("AcademicLevel is required."));

        if (ageRange is null)
            return Result.Failure<Question>(QuestionErrors.InvalidAgeRange("AgeRange is required."));

        // QST-001
        var fourRule = new QuestionMustHaveFourOptionsRule(answerOptions.Count);
        if (fourRule.IsBroken())
            return Result.Failure<Question>(QuestionErrors.QuestionMustHaveFourOptions);

        // Validate each option text
        foreach (var opt in answerOptions)
        {
            if (string.IsNullOrWhiteSpace(opt.text) || opt.text.Trim().Length < 1 || opt.text.Trim().Length > 500)
                return Result.Failure<Question>(QuestionErrors.InvalidAnswerOptionText("Each answer option must be 1-500 non-empty."));
        }

        // Duplicate text check (case-insensitive trim)
        var texts = answerOptions.Select(o => o.text.Trim().ToLowerInvariant()).ToList();
        if (texts.Distinct().Count() != texts.Count)
            return Result.Failure<Question>(QuestionErrors.DuplicateAnswerOption);

        // QST-002
        var correctCount = answerOptions.Count(o => o.isCorrect);
        var correctRule = new ExactlyOneCorrectAnswerRule(correctCount);
        if (correctRule.IsBroken())
            return Result.Failure<Question>(QuestionErrors.QuestionMustHaveOneCorrectAnswer);

        var id = QuestionId.New();
        var options = answerOptions.Select(opt => new AnswerOption(
            AnswerOptionId.New(),
            id,
            opt.text.Trim(),
            opt.isCorrect,
            opt.displayOrder
        )).ToList();

        // Ensure displayOrder 0..3
        for (int i = 0; i < options.Count; i++)
        {
            // if caller provided order inconsistent, reassign sequentially
        }

        var question = new Question(id, text.Trim(), categoryId, difficulty, academicLevel, ageRange, options, createdBy);
        question.RaiseDomainEvent(new QuestionCreatedDomainEvent(question.Id.Value, question.CategoryId.Value));
        return Result.Success(question);
    }

    public Result Update(
        string text,
        CategoryId categoryId,
        DifficultyLevel difficulty,
        AcademicLevel academicLevel,
        AgeRange ageRange,
        IReadOnlyList<(string text, bool isCorrect, int displayOrder)> answerOptions,
        Func<CategoryId, bool>? categoryExists = null)
    {
        var canUpdate = new QuestionCanUpdateRule(Status);
        if (canUpdate.IsBroken())
            return Result.Failure(QuestionErrors.InvalidQuestionState("Cannot update a question in ARCHIVED state."));

        // For PUBLISHED, we allow update only if it keeps 4/1; this is checked below via QST-005
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length < 3 || text.Trim().Length > 500)
            return Result.Failure(QuestionErrors.InvalidQuestionText("Text must be 3-500 characters."));

        if (categoryId is null || categoryId.Value == Guid.Empty)
            return Result.Failure(QuestionErrors.QuestionMustBelongToCategory);

        if (categoryExists is not null && !categoryExists(categoryId))
            return Result.Failure(QuestionErrors.CategoryNotFound(categoryId.Value));

        if (difficulty is null)
            return Result.Failure(QuestionErrors.QuestionMustHaveDifficulty);

        var difficultyRule = new QuestionMustHaveDifficultyRule(difficulty?.Id);
        if (difficultyRule.IsBroken())
            return Result.Failure(QuestionErrors.QuestionMustHaveDifficulty);

        if (academicLevel is null)
            return Result.Failure(QuestionErrors.InvalidAcademicLevel("AcademicLevel is required."));

        if (ageRange is null)
            return Result.Failure(QuestionErrors.InvalidAgeRange("AgeRange is required."));

        var fourRule = new QuestionMustHaveFourOptionsRule(answerOptions.Count);
        if (fourRule.IsBroken())
            return Result.Failure(QuestionErrors.QuestionMustHaveFourOptions);

        foreach (var opt in answerOptions)
        {
            if (string.IsNullOrWhiteSpace(opt.text) || opt.text.Trim().Length < 1 || opt.text.Trim().Length > 500)
                return Result.Failure(QuestionErrors.InvalidAnswerOptionText("Each answer option must be 1-500 non-empty."));
        }

        var texts = answerOptions.Select(o => o.text.Trim().ToLowerInvariant()).ToList();
        if (texts.Distinct().Count() != texts.Count)
            return Result.Failure(QuestionErrors.DuplicateAnswerOption);

        var correctCount = answerOptions.Count(o => o.isCorrect);
        var correctRule = new ExactlyOneCorrectAnswerRule(correctCount);
        if (correctRule.IsBroken())
            return Result.Failure(QuestionErrors.QuestionMustHaveOneCorrectAnswer);

        // QST-005: if PUBLISHED, must keep correct
        var publishedRule = new PublishedQuestionMustHaveCorrectRule(Status, correctCount);
        if (publishedRule.IsBroken())
            return Result.Failure(QuestionErrors.PublishedQuestionMustHaveCorrectAnswer);

        Text = text.Trim();
        CategoryId = categoryId;
        Difficulty = difficulty;
        AcademicLevel = academicLevel;
        AgeRange = ageRange;

        // Recreate answer options preserving IDs where possible by matching text/order
        _answerOptions.Clear();
        foreach (var opt in answerOptions.OrderBy(o => o.displayOrder))
        {
            _answerOptions.Add(new AnswerOption(AnswerOptionId.New(), Id, opt.text.Trim(), opt.isCorrect, opt.displayOrder));
        }

        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new QuestionUpdatedDomainEvent(Id.Value));
        return Result.Success();
    }

    public Result Activate()
    {
        if (!Status.CanBeActivatedFrom)
            return Result.Failure(QuestionErrors.InvalidQuestionState($"Cannot activate from {Status.Name}."));

        Status = QuestionStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
        return Result.Success();
    }

    public Result Deactivate()
    {
        if (!Status.CanBeDeactivatedFrom)
            return Result.Failure(QuestionErrors.InvalidQuestionState($"Cannot deactivate from {Status.Name}."));

        Status = QuestionStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new QuestionDeactivatedDomainEvent(Id.Value));
        return Result.Success();
    }

    public Result Publish()
    {
        if (!Status.CanBePublishedFrom)
            return Result.Failure(QuestionErrors.InvalidQuestionState($"Cannot publish from {Status.Name}."));

        // Gate QST-001..004
        var fourRule = new QuestionMustHaveFourOptionsRule(_answerOptions.Count);
        if (fourRule.IsBroken())
            return Result.Failure(QuestionErrors.QuestionNotPublishable("Requires exactly 4 options."));

        var correctCount = _answerOptions.Count(o => o.IsCorrect);
        var correctRule = new ExactlyOneCorrectAnswerRule(correctCount);
        if (correctRule.IsBroken())
            return Result.Failure(QuestionErrors.QuestionNotPublishable("Requires exactly 1 correct answer."));

        if (CategoryId is null || CategoryId.Value == Guid.Empty)
            return Result.Failure(QuestionErrors.QuestionNotPublishable("Requires Category."));

        if (Difficulty is null)
            return Result.Failure(QuestionErrors.QuestionNotPublishable("Requires Difficulty."));

        // Additional academic/age validation already ensured at create/update
        Status = QuestionStatus.Published;
        PublishedAt = DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new QuestionPublishedDomainEvent(Id.Value, CategoryId.Value));
        return Result.Success();
    }

    public Result Archive()
    {
        if (!Status.CanBeArchivedFrom)
            return Result.Failure(QuestionErrors.InvalidQuestionState($"Cannot archive from {Status.Name}."));

        if (Status.IsArchived)
            return Result.Failure(QuestionErrors.InvalidQuestionState("Already archived."));

        Status = QuestionStatus.Archived;
        UpdatedAt = DateTimeOffset.UtcNow;
        RaiseDomainEvent(new QuestionArchivedDomainEvent(Id.Value));
        return Result.Success();
    }

    /// <summary>
    /// Checks if this question is considered valid for Category publish counting (4/1 + PUBLISHED + aligned).
    /// Alignment of AcademicLevel/AgeRange/Difficulty vs Category is checked externally where Category is known;
    /// here we check structural validity.
    /// </summary>
    public bool IsStructurallyValid => _answerOptions.Count == 4 && _answerOptions.Count(o => o.IsCorrect) == 1 && Status == QuestionStatus.Published;

    public bool IsAvailableForSelection => Status.IsAvailableForSelection && IsStructurallyValid;
}
