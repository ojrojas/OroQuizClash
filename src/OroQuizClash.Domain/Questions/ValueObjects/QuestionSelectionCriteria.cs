using BuildingBlocks.Kernel.Domain.ValueObjects;

using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Domain.Questions.ValueObjects;

public sealed class QuestionSelectionCriteria : ValueObject
{
    public CategoryId? CategoryId { get; }
    public DifficultyLevel? Difficulty { get; }
    public string? AcademicLevel { get; }
    public AgeRange? AgeRange { get; }
    public IReadOnlyList<QuestionId> PreviousQuestionIds { get; }
    public Guid GameId { get; }
    public int? RoundNumber { get; }
    public Guid? RoundId { get; }
    public int Take { get; }

    public QuestionSelectionCriteria(
        CategoryId? categoryId,
        DifficultyLevel? difficulty,
        string? academicLevel,
        AgeRange? ageRange,
        IReadOnlyList<QuestionId> previousQuestionIds,
        Guid gameId,
        int? roundNumber = null,
        Guid? roundId = null,
        int take = 1)
    {
        CategoryId = categoryId;
        Difficulty = difficulty;
        AcademicLevel = academicLevel;
        AgeRange = ageRange;
        PreviousQuestionIds = previousQuestionIds ?? [];
        GameId = gameId;
        RoundNumber = roundNumber;
        RoundId = roundId;
        Take = take < 1 ? 1 : take > 10 ? 10 : take;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return CategoryId?.Value;
        yield return Difficulty?.Id;
        yield return AcademicLevel?.ToLowerInvariant();
        yield return AgeRange?.Min;
        yield return AgeRange?.Max;
        // Sort previous IDs for deterministic equality (order irrelevant)
        foreach (var id in PreviousQuestionIds.OrderBy(x => x.Value))
            yield return id.Value;
        yield return GameId;
        yield return RoundNumber;
        yield return RoundId;
        yield return Take;
    }
}
