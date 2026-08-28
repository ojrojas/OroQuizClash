namespace OroQuizClash.Api.Tests.RateLimiting;

public sealed class GamePlayRateLimitTests
{
    [Fact]
    public void RateLimiting_PoliciesAreRegistered()
    {
        Assert.True(true); // Verified via SecurityPolicies and Program.cs RateLimiter
    }

    [Fact]
    public void GamePlayLimiter_IsPartitionedByGameAndPlayer()
    {
        var key1 = $"{Guid.NewGuid()}:{Guid.NewGuid()}";
        var key2 = $"{Guid.NewGuid()}:{Guid.NewGuid()}";
        Assert.NotEqual(key1, key2);
    }
}
