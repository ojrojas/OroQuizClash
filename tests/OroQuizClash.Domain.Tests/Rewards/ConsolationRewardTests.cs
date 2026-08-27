using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Domain.Tests.Rewards;

public sealed class ConsolationRewardTests
{
    [Fact]
    public void CreateAsConsolation_CreatesApprovedRedemption()
    {
        var playerId = Guid.NewGuid();
        var rewardId = Guid.NewGuid();
        var gameId = Guid.NewGuid();

        var result = RewardRedemption.CreateAsConsolation(playerId, rewardId, gameId);

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Approved, result.Value.Status);
        Assert.Equal(0, result.Value.Points);
        Assert.NotNull(result.Value.DeliveredAt);
        Assert.Single(result.Value.Transitions);
        Assert.Equal(RedemptionStatus.Approved, result.Value.Transitions.First().Status);
    }

    [Fact]
    public void CreateAsConsolation_EmptyPlayerId_ReturnsFailure()
    {
        var result = RewardRedemption.CreateAsConsolation(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void CreateAsConsolation_EmptyRewardId_ReturnsFailure()
    {
        var result = RewardRedemption.CreateAsConsolation(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void CreateAsConsolation_EmptyGameId_ReturnsFailure()
    {
        var result = RewardRedemption.CreateAsConsolation(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
        Assert.False(result.IsSuccess);
    }
}
