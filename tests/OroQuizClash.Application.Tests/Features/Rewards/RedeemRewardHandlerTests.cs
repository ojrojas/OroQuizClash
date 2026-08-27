using BuildingBlocks.Kernel.Domain.Repositories;

using NSubstitute;

using OroQuizClash.Application.Features.Rewards;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Rewards;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Tests.Features.Rewards;

public sealed class RedeemRewardHandlerTests
{
    private readonly IRepository<Reward, RewardId> _rewardRepo = Substitute.For<IRepository<Reward, RewardId>>();
    private readonly IRepository<Game, GameId> _gameRepo = Substitute.For<IRepository<Game, GameId>>();
    private readonly IRepository<RewardRedemption, RewardRedemptionId> _redemptionRepo = Substitute.For<IRepository<RewardRedemption, RewardRedemptionId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Redeem_SufficientPoints_CreatesRedemption()
    {
        var reward = Reward.Create("Prize", "A prize", 100, 5).Value;
        var game = CreateGameWithPlayer(out var playerId, 200);

        _rewardRepo.FirstOrDefaultAsync(Arg.Any<RewardByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(reward);
        _gameRepo.FirstOrDefaultAsync(Arg.Any<GameByIdWithAnswersSpecification>(), Arg.Any<CancellationToken>())
            .Returns(game);
        _redemptionRepo.FirstOrDefaultAsync(Arg.Any<RedemptionByIdempotencyKeySpecification>(), Arg.Any<CancellationToken>())
            .Returns((RewardRedemption?)null);

        var handler = new RedeemRewardHandler(_rewardRepo, _gameRepo, _redemptionRepo, _unitOfWork);
        var command = new RedeemRewardCommand(reward.Id.Value, game.Id.Value, playerId, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(4, reward.Stock);
        Assert.Equal(100, game.GetPlayerScore(playerId).CurrentPoints);
    }

    [Fact]
    public async Task Redeem_InsufficientPoints_ReturnsFailure()
    {
        var reward = Reward.Create("Prize", "A prize", 100, 5).Value;
        var game = CreateGameWithPlayer(out var playerId, 50);

        _rewardRepo.FirstOrDefaultAsync(Arg.Any<RewardByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(reward);
        _gameRepo.FirstOrDefaultAsync(Arg.Any<GameByIdWithAnswersSpecification>(), Arg.Any<CancellationToken>())
            .Returns(game);
        _redemptionRepo.FirstOrDefaultAsync(Arg.Any<RedemptionByIdempotencyKeySpecification>(), Arg.Any<CancellationToken>())
            .Returns((RewardRedemption?)null);

        var handler = new RedeemRewardHandler(_rewardRepo, _gameRepo, _redemptionRepo, _unitOfWork);
        var command = new RedeemRewardCommand(reward.Id.Value, game.Id.Value, playerId, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(5, reward.Stock);
    }

    [Fact]
    public async Task Redeem_OutOfStock_ReturnsFailure()
    {
        var reward = Reward.Create("Prize", "A prize", 100, 0).Value;
        var game = CreateGameWithPlayer(out var playerId, 200);

        _rewardRepo.FirstOrDefaultAsync(Arg.Any<RewardByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(reward);

        var handler = new RedeemRewardHandler(_rewardRepo, _gameRepo, _redemptionRepo, _unitOfWork);
        var command = new RedeemRewardCommand(reward.Id.Value, game.Id.Value, playerId, null);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Redeem_DuplicateIdempotencyKey_ReturnsExisting()
    {
        var reward = Reward.Create("Prize", "A prize", 100, 5).Value;
        var game = CreateGameWithPlayer(out var playerId, 200);
        var existingKey = Guid.NewGuid();
        var existing = RewardRedemption.Create(playerId, reward.Id, game.Id, 100, existingKey).Value;

        _rewardRepo.FirstOrDefaultAsync(Arg.Any<RewardByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(reward);
        _redemptionRepo.FirstOrDefaultAsync(Arg.Any<RedemptionByIdempotencyKeySpecification>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var handler = new RedeemRewardHandler(_rewardRepo, _gameRepo, _redemptionRepo, _unitOfWork);
        var command = new RedeemRewardCommand(reward.Id.Value, game.Id.Value, playerId, existingKey);

        var result = await handler.HandleAsync(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(existing.Id.Value, result.Value.RedemptionId);
        Assert.Equal(5, reward.Stock);
    }

    private static Game CreateGameWithPlayer(out Guid playerId, int initialPoints)
    {
        playerId = Guid.NewGuid();
        var config = new GameConfiguration(
            "Test Game",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear,
            30,
            ScoringSystem.Standard,
            LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore,
            ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10, 100);
        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        game.JoinPlayer(playerId, "Player1");
        game.Start();

        if (initialPoints > 0)
        {
            game.AdjustPoints(playerId, initialPoints, "Test setup", Guid.NewGuid());
        }

        return game;
    }
}
