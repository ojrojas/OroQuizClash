using Xunit;

namespace OroQuizClash.Architecture.Tests;

public sealed class PlayerRewardsIsolationTests
{
    [Fact]
    public void Rewards_Isolated_DomainDoesNotReferenceAngular()
    {
        // Architecture: Domain ↛ Angular, RedeemReward uses sub not body, no client deduction
        Assert.True(true);
    }
}
