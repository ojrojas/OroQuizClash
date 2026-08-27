using OroQuizClash.Domain.Rewards;
using OroQuizClash.Domain.Rewards.Events;

namespace OroQuizClash.Domain.Tests.Rewards;

public sealed class RewardAvailabilityTests
{
    [Fact]
    public void Create_DefaultsToActive()
    {
        var result = Reward.Create("Test Reward", "Description", 100, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(RewardStatus.Active, result.Value.Status);
        Assert.Equal(5, result.Value.Stock);
    }

    [Fact]
    public void Create_InvalidName_ReturnsFailure()
    {
        var result = Reward.Create("ab", "Description", 100, 5);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_ZeroPoints_ReturnsFailure()
    {
        var result = Reward.Create("Reward", "Description", 0, 5);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_NegativeStock_ReturnsFailure()
    {
        var result = Reward.Create("Reward", "Description", 100, -1);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ReserveStock_ActiveWithStock_Decrements()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5).Value;

        var result = reward.ReserveStock(DateTimeOffset.UtcNow);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, reward.Stock);
    }

    [Fact]
    public void ReserveStock_Inactive_ReturnsFailure()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5).Value;
        reward.Deactivate();

        var result = reward.ReserveStock(DateTimeOffset.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal(5, reward.Stock);
    }

    [Fact]
    public void ReserveStock_ZeroStock_ReturnsFailure()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 0).Value;

        var result = reward.ReserveStock(DateTimeOffset.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal(0, reward.Stock);
    }

    [Fact]
    public void ReserveStock_Expired_ReturnsFailure()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5, DateTimeOffset.UtcNow.AddDays(-1)).Value;

        var result = reward.ReserveStock(DateTimeOffset.UtcNow);

        Assert.False(result.IsSuccess);
        Assert.Equal(5, reward.Stock);
    }

    [Fact]
    public void ReleaseStock_Increments()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5).Value;
        reward.ReserveStock(DateTimeOffset.UtcNow);

        reward.ReleaseStock();

        Assert.Equal(5, reward.Stock);
    }

    [Fact]
    public void IsAvailable_ActiveInStockNotExpired_True()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5).Value;

        Assert.True(reward.IsAvailable(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsAvailable_Inactive_False()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5).Value;
        reward.Deactivate();

        Assert.False(reward.IsAvailable(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsAvailable_ZeroStock_False()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 0).Value;

        Assert.False(reward.IsAvailable(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void IsAvailable_Expired_False()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5, DateTimeOffset.UtcNow.AddDays(-1)).Value;

        Assert.False(reward.IsAvailable(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Update_ChangesFields()
    {
        var reward = Reward.Create("Old Name", "Old Desc", 100, 5).Value;

        var result = reward.Update(name: "New Name", pointsRequired: 200);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", reward.Name);
        Assert.Equal(200, reward.PointsRequired);
        Assert.NotNull(reward.UpdatedAt);
    }

    [Fact]
    public void Activate_DeactivatedReward_Success()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5).Value;
        reward.Deactivate();

        var result = reward.Activate();

        Assert.True(result.IsSuccess);
        Assert.Equal(RewardStatus.Active, reward.Status);
    }

    [Fact]
    public void Activate_AlreadyActive_ReturnsFailure()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5).Value;

        var result = reward.Activate();

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Deactivate_AlreadyInactive_ReturnsFailure()
    {
        var reward = Reward.Create("Reward", "Desc", 100, 5).Value;
        reward.Deactivate();

        var result = reward.Deactivate();

        Assert.False(result.IsSuccess);
    }
}
