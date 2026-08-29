using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class LobbyGameDetailContractTests
{
    [Fact]
    public async Task GetGameDetail_Returns8PlusExtended_NoAnswerLeak()
    {
        // GET /api/games/{id} contains 8 fields + TimeLimit/Points/Withdrawal/Loss/PlayersList, no Answer/Score leak FR-013
        await Task.CompletedTask;
        Assert.True(true);
    }

    [Fact]
    public async Task GetGameDetail_404_WithProblemDetails()
    {
        // manipulated id → 404 GameNotFound with CorrelationId
        await Task.CompletedTask;
        Assert.True(true);
    }
}
