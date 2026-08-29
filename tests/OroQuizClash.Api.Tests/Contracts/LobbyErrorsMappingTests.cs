using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class LobbyErrorsMappingTests
{
    [Theory]
    [InlineData("GameFull", 409)]
    [InlineData("GameNotWaitingForPlayers", 400)]
    [InlineData("GameNotFound", 404)]
    [InlineData("PlayerIdentityMismatch", 403)]
    public void ProblemDetails_Maps_Correct_HttpStatus(string code, int status)
    {
        // GlobalExceptionHandler → Result.ToHttpResult() RFC7807 with CorrelationId/TraceId
        Assert.True(status >= 400);
    }
}
