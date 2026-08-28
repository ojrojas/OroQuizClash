using System.Text.Json;

namespace OroQuizClash.Api.Tests.Authorization;

public sealed class AntiTamperingContractTests
{
    [Fact]
    public void SubmitAnswerRequest_DoesNotContainScoreFields()
    {
        var json = JsonSerializer.Serialize(new { answerOptionId = Guid.NewGuid(), score = 9999, gameState = "FINISHED", correctness = true });
        // Server should ignore extra fields; DTO only has AnswerOptionId
        var props = typeof(OroQuizClash.Application.Features.Games.SubmitAnswerCommand).GetProperties().Select(p => p.Name).ToList();
        Assert.DoesNotContain("Score", props);
        Assert.DoesNotContain("GameState", props);
    }

    [Fact]
    public void QuestionPresentedPayload_DoesNotExposeIsCorrect()
    {
        var payload = new OroQuizClash.Application.Features.Games.Notifications.QuestionPresentedPayload(Guid.NewGuid(), "Q", [new(Guid.NewGuid(), "A")]);
        var json = JsonSerializer.Serialize(payload);
        Assert.DoesNotContain("IsCorrect", json, StringComparison.OrdinalIgnoreCase);
    }
}
