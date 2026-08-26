using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Infrastructure.Counters;

public sealed class InMemoryQuestionCounter : IQuestionCounter
{
    private readonly Dictionary<CategoryId, List<QuestionStub>> _store = new();

    public Task<int> CountValidAsync(CategoryId categoryId, CancellationToken cancellationToken = default)
    {
        if (!_store.TryGetValue(categoryId, out var list))
        {
            return Task.FromResult(0);
        }

        var count = list.Count(q => q.IsValid);
        return Task.FromResult(count);
    }

    /// <summary>Seed the counter with <paramref name="count"/> valid question stubs for tests/quickstart.</summary>
    public void Seed(CategoryId categoryId, int count)
    {
        var list = new List<QuestionStub>(count);
        for (var i = 0; i < count; i++)
        {
            list.Add(QuestionStub.CreateValid(categoryId));
        }

        _store[categoryId] = list;
    }

    public void AddQuestion(QuestionStub stub)
    {
        if (!_store.TryGetValue(stub.CategoryId, out var list))
        {
            list = new List<QuestionStub>();
            _store[stub.CategoryId] = list;
        }

        list.Add(stub);
    }

    public void Clear() => _store.Clear();

    public void Clear(CategoryId categoryId) => _store.Remove(categoryId);
}

/// <summary>
/// Minimal question stub for counting valid questions.
/// Valid when: 4 options, exactly 1 correct, active, and aligned to category.
/// Alignment (Difficulty/AcademicLevel/AgeRange) is considered true for seeded valid stubs;
/// desalineadas can be constructed with <c>IsAligned = false</c> to simulate invalid alignment.
/// </summary>
public sealed record QuestionStub(
    CategoryId CategoryId,
    int AnswerOptionCount,
    int CorrectCount,
    bool IsActive,
    bool IsAligned = true,
    int Difficulty = 3,
    string? AcademicLevel = null)
{
    public bool IsValid => AnswerOptionCount == 4
        && CorrectCount == 1
        && IsActive
        && IsAligned;

    public static QuestionStub CreateValid(CategoryId categoryId) =>
        new(categoryId, 4, 1, true, true);

    public static QuestionStub CreateInvalidOptions(CategoryId categoryId) =>
        new(categoryId, 3, 1, true, true);

    public static QuestionStub CreateInvalidCorrect(CategoryId categoryId, int correctCount) =>
        new(categoryId, 4, correctCount, true, true);

    public static QuestionStub CreateInactive(CategoryId categoryId) =>
        new(categoryId, 4, 1, false, true);

    public static QuestionStub CreateMisaligned(CategoryId categoryId) =>
        new(categoryId, 4, 1, true, false);
}