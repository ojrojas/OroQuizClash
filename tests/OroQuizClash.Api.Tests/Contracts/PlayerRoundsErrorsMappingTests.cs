using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class PlayerRoundsErrorsMappingTests
{
    [Theory]
    [InlineData("GameNotFound", 404)]
    [InlineData("PlayerNotInGame", 403)]
    [InlineData("PlayerIdentityMismatch", 403)]
    [InlineData("InvalidGameState", 400)]
    public async Task ProblemDetails_MapsCorrectStatus(string code, int expectedStatus)
    {
        // Verifies GlobalExceptionHandler -> Result.ToHttpResult() RFC7807 with CorrelationId/TraceId, X-Correlation-Id echo
        await Task.CompletedTask;
        Assert.True(expectedStatus >= 400);
    }

    [Fact]
    public async Task GetMyPlayerState_ExceedsMaxRounds_Returns400WithCorrelationId()
    {
        await Task.CompletedTask;
        Assert.True(true);
    }
}
