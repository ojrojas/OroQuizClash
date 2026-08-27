using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.Events;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Domain.Tests.Questions;

public sealed class QuestionLifecycleTests
{
    private static CategoryId AnyCategoryId() => new(Guid.NewGuid());

    private static Domain.Questions.Question CreateDraftQuestion(
        int optionCount = 4,
        int correctCount = 1)
    {
        var options = new List<(string text, bool isCorrect, int displayOrder)>();
        for (int i = 0; i < optionCount; i++)
        {
            bool isCorrect = i < correctCount;
            options.Add(($"Option {i + 1}", isCorrect, i));
        }

        var result = Domain.Questions.Question.Create(
            "What is the capital of France?",
            AnyCategoryId(),
            DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            options,
            Guid.NewGuid());

        return result.Value;
    }

    [Fact]
    public void Publish_FromDraft_SucceedsAndRaisesDomainEvent()
    {
        var question = CreateDraftQuestion();

        var result = question.Publish();

        Assert.True(result.IsSuccess);
        Assert.Equal(QuestionStatus.Published, question.Status);
        Assert.NotNull(question.PublishedAt);
        Assert.Contains(question.DomainEvents, e => e is QuestionPublishedDomainEvent);
    }

    [Fact]
    public void Publish_FromActive_Succeeds()
    {
        var question = CreateDraftQuestion();
        question.Activate();

        var result = question.Publish();

        Assert.True(result.IsSuccess);
        Assert.Equal(QuestionStatus.Published, question.Status);
        Assert.Contains(question.DomainEvents, e => e is QuestionPublishedDomainEvent);
    }

    [Fact]
    public void Deactivate_FromPublished_Succeeds()
    {
        var question = CreateDraftQuestion();
        question.Publish();

        var result = question.Deactivate();

        Assert.True(result.IsSuccess);
        Assert.Equal(QuestionStatus.Inactive, question.Status);
        Assert.Contains(question.DomainEvents, e => e is QuestionDeactivatedDomainEvent);
    }

    [Fact]
    public void Publish_FromArchived_FailsBecauseArchivedIsTerminal()
    {
        var question = CreateDraftQuestion();
        question.Publish();
        question.Deactivate();
        question.Archive();

        var result = question.Publish();

        Assert.True(result.IsFailure);
        Assert.Contains("InvalidState", result.Error.Code);
    }
}
