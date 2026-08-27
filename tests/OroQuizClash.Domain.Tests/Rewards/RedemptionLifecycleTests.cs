using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Domain.Tests.Rewards;

public sealed class RedemptionLifecycleTests
{
    private static readonly RewardId TestRewardId = new(Guid.NewGuid());
    private static readonly GameId TestGameId = new(Guid.NewGuid());

    [Fact]
    public void Create_SetsRequestedWithTransition()
    {
        var playerId = Guid.NewGuid();
        var result = RewardRedemption.Create(playerId, TestRewardId, TestGameId, 100);

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Requested, result.Value.Status);
        Assert.Single(result.Value.Transitions);
        Assert.Equal(playerId, result.Value.Transitions.First().ActorId);
    }

    [Fact]
    public void Approve_FromRequested_Success()
    {
        var redemption = RewardRedemption.Create(Guid.NewGuid(), TestRewardId, TestGameId, 100).Value;
        var managerId = Guid.NewGuid();

        var result = redemption.Approve(managerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Approved, redemption.Status);
        Assert.Equal(2, redemption.Transitions.Count);
        Assert.Equal(managerId, redemption.Transitions.Last().ActorId);
    }

    [Fact]
    public void Approve_FromNonRequested_ReturnsFailure()
    {
        var redemption = RewardRedemption.Create(Guid.NewGuid(), TestRewardId, TestGameId, 100).Value;
        redemption.Approve(Guid.NewGuid());

        var result = redemption.Approve(Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Reject_FromRequested_Success()
    {
        var redemption = RewardRedemption.Create(Guid.NewGuid(), TestRewardId, TestGameId, 100).Value;

        var result = redemption.Reject(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Rejected, redemption.Status);
        Assert.True(redemption.Status.IsTerminal);
    }

    [Fact]
    public void Deliver_FromApproved_SetsDeliveredAt()
    {
        var redemption = RewardRedemption.Create(Guid.NewGuid(), TestRewardId, TestGameId, 100).Value;
        redemption.Approve(Guid.NewGuid());

        var result = redemption.Deliver(Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Delivered, redemption.Status);
        Assert.NotNull(redemption.DeliveredAt);
        Assert.True(redemption.Status.IsTerminal);
    }

    [Fact]
    public void Deliver_FromNonApproved_ReturnsFailure()
    {
        var redemption = RewardRedemption.Create(Guid.NewGuid(), TestRewardId, TestGameId, 100).Value;

        var result = redemption.Deliver(Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Cancel_FromRequested_ByOwner_Success()
    {
        var playerId = Guid.NewGuid();
        var redemption = RewardRedemption.Create(playerId, TestRewardId, TestGameId, 100).Value;

        var result = redemption.Cancel(playerId);

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Cancelled, redemption.Status);
        Assert.True(redemption.Status.IsTerminal);
    }

    [Fact]
    public void Cancel_ByNonOwner_ReturnsFailure()
    {
        var playerId = Guid.NewGuid();
        var redemption = RewardRedemption.Create(playerId, TestRewardId, TestGameId, 100).Value;

        var result = redemption.Cancel(Guid.NewGuid());

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Cancel_FromDelivered_ReturnsFailure()
    {
        var playerId = Guid.NewGuid();
        var redemption = RewardRedemption.Create(playerId, TestRewardId, TestGameId, 100).Value;
        redemption.Approve(Guid.NewGuid());
        redemption.Deliver(Guid.NewGuid());

        var result = redemption.Cancel(playerId);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void TerminalStates_RejectAllTransitions()
    {
        var playerId = Guid.NewGuid();
        var redemption = RewardRedemption.Create(playerId, TestRewardId, TestGameId, 100).Value;
        redemption.Reject(Guid.NewGuid());

        Assert.False(redemption.Approve(Guid.NewGuid()).IsSuccess);
        Assert.False(redemption.Deliver(Guid.NewGuid()).IsSuccess);
        Assert.False(redemption.Cancel(playerId).IsSuccess);
    }

    [Fact]
    public void EveryTransition_AppendsRecord()
    {
        var playerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var redemption = RewardRedemption.Create(playerId, TestRewardId, TestGameId, 100).Value;

        redemption.Approve(managerId);
        redemption.Deliver(managerId);

        Assert.Equal(3, redemption.Transitions.Count);
        Assert.Equal(RedemptionStatus.Requested, redemption.Transitions.ElementAt(0).Status);
        Assert.Equal(RedemptionStatus.Approved, redemption.Transitions.ElementAt(1).Status);
        Assert.Equal(RedemptionStatus.Delivered, redemption.Transitions.ElementAt(2).Status);
        Assert.All(redemption.Transitions, t => Assert.NotEqual(Guid.Empty, t.ActorId));
    }
}
