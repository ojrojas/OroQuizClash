using NSubstitute;

using OroQuizClash.Application.Features.Games.Notifications;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class RealtimePayloadFilteringTests
{
    [Fact]
    public void QuestionPresentedPayload_DoesNotContainIsCorrect()
    {
        var payload = new QuestionPresentedPayload(
            Guid.NewGuid(),
            "Test question?",
            new[]
            {
                new QuestionOptionPayload(Guid.NewGuid(), "Option A"),
                new QuestionOptionPayload(Guid.NewGuid(), "Option B"),
                new QuestionOptionPayload(Guid.NewGuid(), "Option C"),
                new QuestionOptionPayload(Guid.NewGuid(), "Option D")
            });

        var json = System.Text.Json.JsonSerializer.Serialize(payload);
        Assert.DoesNotContain("IsCorrect", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correct", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("is_correct", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QuestionPresentedPayload_ContainsOnlyIdAndTextPerOption()
    {
        var optionId = Guid.NewGuid();
        var payload = new QuestionPresentedPayload(
            Guid.NewGuid(),
            "Test question?",
            new[] { new QuestionOptionPayload(optionId, "Answer") });

        Assert.Single(payload.AnswerOptions);
        Assert.Equal(optionId, payload.AnswerOptions[0].Id);
        Assert.Equal("Answer", payload.AnswerOptions[0].Text);
        Assert.Equal(2, typeof(QuestionOptionPayload).GetProperties().Length);
    }

    [Fact]
    public void QuestionOptionPayload_DoesNotExposeIsCorrectProperty()
    {
        var props = typeof(QuestionOptionPayload).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("IsCorrect", props);
        Assert.Contains("Id", props);
        Assert.Contains("Text", props);
    }

    [Fact]
    public void PlayerAnswered_PayloadShape_DoesNotContainForbiddenFields()
    {
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var roundId = Guid.NewGuid();
        var answeredAt = DateTimeOffset.UtcNow;

        var payload = new { gameId, playerId, roundId, answeredAt };
        var json = System.Text.Json.JsonSerializer.Serialize(payload);

        Assert.DoesNotContain("AnswerOptionId", json);
        Assert.DoesNotContain("correct", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"points\"", json, StringComparison.OrdinalIgnoreCase);
    }
}
