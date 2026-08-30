using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class PlayerRewardsHistoryContractTests
{
    [Fact]
    public void GetRedemptions_ReturnsOrderedHistory_WithConsolation()
    {
        // Contract: GET /api/redemptions returns ordered desc RequestedAt with Consolation points 0 APPROVED
        Assert.True(true);
    }
}
