using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions.Events;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Domain.Tests.Questions;

public sealed class QuestionUpdateTests
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

    private static Domain.Questions.Question CreateDraftQuestion()
    {
        var result = Domain.Questions.Question.Create(
            "What is the capital of France?",
            AnyCategoryId(),
            DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            ValidOptions(),
            Guid.NewGuid());

        return result.Value;
    }

    [Fact]
    public void Update_FromDraft_SucceedsAndSetsUpdatedAt()
    {
        var question = CreateDraftQuestion();
        var beforeUpdate = question.UpdatedAt;

        var result = question.Update(
            "What is the capital of Spain?",
            AnyCategoryId(),
            DifficultyLevel.Elementary,
            AcademicLevel.Create("Secondary"),
            AgeRange.Create(11, 14),
            ValidOptions());

        Assert.True(result.IsSuccess);
        Assert.Equal("What is the capital of Spain?", question.Text);
        Assert.True(question.UpdatedAt >= beforeUpdate);
        Assert.Contains(question.DomainEvents, e => e is QuestionUpdatedDomainEvent);
    }

    [Fact]
    public void Update_FromArchived_FailsInvalidQuestionState()
    {
        var question = CreateDraftQuestion();
        question.Publish();
        question.Deactivate();
        question.Archive();

        var result = question.Update(
            "What is the capital of Spain?",
            AnyCategoryId(),
            DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            ValidOptions());

        Assert.True(result.IsFailure);
        Assert.Contains("InvalidState", result.Error.Code);
    }
}
