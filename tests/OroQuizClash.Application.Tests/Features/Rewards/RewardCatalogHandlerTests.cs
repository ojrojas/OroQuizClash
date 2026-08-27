using BuildingBlocks.Kernel.Domain.Repositories;

using NSubstitute;

using OroQuizClash.Application.Features.Rewards;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Rewards;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Tests.Features.Rewards;

public sealed class RewardCatalogHandlerTests
{
    private readonly IRepository<Reward, RewardId> _rewardRepo = Substitute.For<IRepository<Reward, RewardId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task CreateReward_Success()
    {
        _rewardRepo.AddAsync(Arg.Any<Reward>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var handler = new CreateRewardHandler(_rewardRepo, _unitOfWork);
        var result = await handler.HandleAsync(
            new CreateRewardCommand("Prize", "A prize", 100, 5, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Prize", result.Value.Name);
        Assert.Equal("ACTIVE", result.Value.Status);
    }

    [Fact]
    public async Task CreateReward_InvalidName_ReturnsFailure()
    {
        var handler = new CreateRewardHandler(_rewardRepo, _unitOfWork);
        var result = await handler.HandleAsync(
            new CreateRewardCommand("ab", "Desc", 100, 5, null),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task ActivateReward_Success()
    {
        var reward = Reward.Create("Prize", "Desc", 100, 5).Value;
        reward.Deactivate();
        _rewardRepo.FirstOrDefaultAsync(Arg.Any<RewardByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(reward);

        var handler = new ActivateRewardHandler(_rewardRepo, _unitOfWork);
        var result = await handler.HandleAsync(
            new ActivateRewardCommand(reward.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("ACTIVE", result.Value.Status);
    }

    [Fact]
    public async Task DeactivateReward_Success()
    {
        var reward = Reward.Create("Prize", "Desc", 100, 5).Value;
        _rewardRepo.FirstOrDefaultAsync(Arg.Any<RewardByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(reward);

        var handler = new DeactivateRewardHandler(_rewardRepo, _unitOfWork);
        var result = await handler.HandleAsync(
            new DeactivateRewardCommand(reward.Id.Value),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("INACTIVE", result.Value.Status);
    }

    [Fact]
    public async Task GetRewards_ReturnsAvailableRewards()
    {
        var reward = Reward.Create("Prize", "Desc", 100, 5).Value;
        _rewardRepo.ListAsync(Arg.Any<AvailableRewardsSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new List<Reward> { reward });

        var handler = new GetRewardsHandler(_rewardRepo, Substitute.For<IRepository<Game, GameId>>());
        var result = await handler.HandleAsync(
            new GetRewardsQuery(null, false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Rewards);
    }
}
