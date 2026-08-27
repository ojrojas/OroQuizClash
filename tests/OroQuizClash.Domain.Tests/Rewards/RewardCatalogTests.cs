using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Domain.Tests.Rewards;

public sealed class RewardCatalogTests
{
    [Fact]
    public void Create_InvalidName_ReturnsFailure()
    {
        var result = Reward.Create("ab", "Desc", 100, 5);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_ZeroPoints_ReturnsFailure()
    {
        var result = Reward.Create("Name", "Desc", 0, 5);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_NegativeStock_ReturnsFailure()
    {
        var result = Reward.Create("Name", "Desc", 100, -1);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Create_ExpirationInPast_ReturnsSuccess()
    {
        var result = Reward.Create("Name", "Desc", 100, 5, DateTimeOffset.UtcNow.AddDays(-1));
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Update_Name_ReturnsSuccess()
    {
        var reward = Reward.Create("Old Name", "Desc", 100, 5).Value;
        var result = reward.Update(name: "New Name");
        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", reward.Name);
        Assert.NotNull(reward.UpdatedAt);
    }

    [Fact]
    public void Update_PointsRequired_ReturnsSuccess()
    {
        var reward = Reward.Create("Name", "Desc", 100, 5).Value;
        var result = reward.Update(pointsRequired: 200);
        Assert.True(result.IsSuccess);
        Assert.Equal(200, reward.PointsRequired);
    }

    [Fact]
    public void Activate_FromInactive_Success()
    {
        var reward = Reward.Create("Name", "Desc", 100, 5).Value;
        reward.Deactivate();
        var result = reward.Activate();
        Assert.True(result.IsSuccess);
        Assert.Equal(RewardStatus.Active, reward.Status);
    }

    [Fact]
    public void Activate_FromActive_ReturnsFailure()
    {
        var reward = Reward.Create("Name", "Desc", 100, 5).Value;
        var result = reward.Activate();
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Deactivate_FromActive_Success()
    {
        var reward = Reward.Create("Name", "Desc", 100, 5).Value;
        var result = reward.Deactivate();
        Assert.True(result.IsSuccess);
        Assert.Equal(RewardStatus.Inactive, reward.Status);
    }

    [Fact]
    public void Deactivate_FromInactive_ReturnsFailure()
    {
        var reward = Reward.Create("Name", "Desc", 100, 5).Value;
        reward.Deactivate();
        var result = reward.Deactivate();
        Assert.False(result.IsSuccess);
    }
}
