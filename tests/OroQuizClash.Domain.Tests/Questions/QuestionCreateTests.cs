using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Domain.Tests.Questions;

public sealed class QuestionCreateTests
{
    private static CategoryId AnyCategoryId() => new(Guid.NewGuid());

    private static IReadOnlyList<(string text, bool isCorrect, int displayOrder)> ValidOptions()
    {
        return
        [
            ("Paris is the capital of France", true, 0),
            ("London is the capital of France", false, 1),
            ("Berlin is the capital of France", false, 2),
            ("Rome is the capital of France", false, 3)
        ];
    }

    [Fact]
    public void Create_WithValidFourOptionsOneCorrect_Succeeds()
    {
        var result = Domain.Questions.Question.Create(
            "What is the capital of France?",
            AnyCategoryId(),
            DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            ValidOptions(),
            Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal(QuestionStatus.Draft, result.Value.Status);
        Assert.Equal(4, result.Value.AnswerOptions.Count);
        Assert.Single(result.Value.AnswerOptions.Where(o => o.IsCorrect));
    }

    [Fact]
    public void Create_WithThreeOptions_FailsQuestionMustHaveFourOptions()
    {
        var options = new (string text, bool isCorrect, int displayOrder)[]
        {
            ("Option A", true, 0),
            ("Option B", false, 1),
            ("Option C", false, 2)
        };

        var result = Domain.Questions.Question.Create(
            "Test question?",
            AnyCategoryId(),
            DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            options,
            Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Question.MustHaveFourOptions", result.Error.Code);
    }

    [Fact]
    public void Create_WithZeroCorrectAnswers_FailsExactlyOneCorrectAnswer()
    {
        var options = new (string text, bool isCorrect, int displayOrder)[]
        {
            ("Option A", false, 0),
            ("Option B", false, 1),
            ("Option C", false, 2),
            ("Option D", false, 3)
        };

        var result = Domain.Questions.Question.Create(
            "Test question?",
            AnyCategoryId(),
            DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            options,
            Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Question.MustHaveOneCorrectAnswer", result.Error.Code);
    }

    [Fact]
    public void Create_WithTwoCorrectAnswers_FailsExactlyOneCorrectAnswer()
    {
        var options = new (string text, bool isCorrect, int displayOrder)[]
        {
            ("Option A", true, 0),
            ("Option B", true, 1),
            ("Option C", false, 2),
            ("Option D", false, 3)
        };

        var result = Domain.Questions.Question.Create(
            "Test question?",
            AnyCategoryId(),
            DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            options,
            Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Question.MustHaveOneCorrectAnswer", result.Error.Code);
    }
}
