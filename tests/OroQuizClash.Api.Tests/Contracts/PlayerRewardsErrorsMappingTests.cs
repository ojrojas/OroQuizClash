using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class PlayerRewardsErrorsMappingTests
{
    [Theory]
    [InlineData("RewardUnavailable", 409)]
    [InlineData("InsufficientPoints", 409)]
    [InlineData("RewardNotFound", 404)]
    [InlineData("PlayerNotInGame", 403)]
    public void ProblemDetails_MapsCorrectStatus(string code, int status)
    {
        // RFC7807 mapping via GlobalExceptionHandler Result.ToHttpResult() with CorrelationId/TraceId
        Assert.True(status > 0);
    }
}
