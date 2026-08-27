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

public sealed class RedemptionProcessingHandlerTests
{
    private readonly IRepository<RewardRedemption, RewardRedemptionId> _redemptionRepo = Substitute.For<IRepository<RewardRedemption, RewardRedemptionId>>();
    private readonly IRepository<Reward, RewardId> _rewardRepo = Substitute.For<IRepository<Reward, RewardId>>();
    private readonly IRepository<Game, GameId> _gameRepo = Substitute.For<IRepository<Game, GameId>>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    [Fact]
    public async Task Approve_FromRequested_Success()
    {
        var redemption = CreateRedemption(RedemptionStatus.Requested);
        _redemptionRepo.FirstOrDefaultAsync(Arg.Any<RedemptionByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(redemption);

        var handler = new ApproveRedemptionHandler(_redemptionRepo, _unitOfWork);
        var result = await handler.HandleAsync(new ApproveRedemptionCommand(redemption.Id.Value, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Approved, redemption.Status);
    }

    [Fact]
    public async Task Reject_RefundsAndReleasesStock()
    {
        var reward = Reward.Create("Prize", "Desc", 100, 4).Value;
        var game = CreateGameWithPlayer(out var playerId, 200);
        var redemption = RewardRedemption.Create(playerId, reward.Id, game.Id, 100).Value;

        _redemptionRepo.FirstOrDefaultAsync(Arg.Any<RedemptionByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(redemption);
        _rewardRepo.FirstOrDefaultAsync(Arg.Any<RewardByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(reward);
        _gameRepo.FirstOrDefaultAsync(Arg.Any<GameByIdWithAnswersSpecification>(), Arg.Any<CancellationToken>())
            .Returns(game);

        var handler = new RejectRedemptionHandler(_redemptionRepo, _rewardRepo, _gameRepo, _unitOfWork);
        var result = await handler.HandleAsync(new RejectRedemptionCommand(redemption.Id.Value, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Rejected, redemption.Status);
        Assert.Equal(5, reward.Stock);
        Assert.Equal(300, game.GetPlayerScore(playerId).CurrentPoints);
    }

    [Fact]
    public async Task Cancel_ByOwner_RefundsAndReleasesStock()
    {
        var reward = Reward.Create("Prize", "Desc", 100, 4).Value;
        var game = CreateGameWithPlayer(out var playerId, 200);
        var redemption = RewardRedemption.Create(playerId, reward.Id, game.Id, 100).Value;

        _redemptionRepo.FirstOrDefaultAsync(Arg.Any<RedemptionByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(redemption);
        _rewardRepo.FirstOrDefaultAsync(Arg.Any<RewardByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(reward);
        _gameRepo.FirstOrDefaultAsync(Arg.Any<GameByIdWithAnswersSpecification>(), Arg.Any<CancellationToken>())
            .Returns(game);

        var handler = new CancelRedemptionHandler(_redemptionRepo, _rewardRepo, _gameRepo, _unitOfWork);
        var result = await handler.HandleAsync(new CancelRedemptionCommand(redemption.Id.Value, playerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(RedemptionStatus.Cancelled, redemption.Status);
        Assert.Equal(5, reward.Stock);
        Assert.Equal(300, game.GetPlayerScore(playerId).CurrentPoints);
    }

    [Fact]
    public async Task Cancel_ByNonOwner_ReturnsFailure()
    {
        var playerId = Guid.NewGuid();
        var redemption = RewardRedemption.Create(playerId, new RewardId(Guid.NewGuid()), new GameId(Guid.NewGuid()), 100).Value;

        _redemptionRepo.FirstOrDefaultAsync(Arg.Any<RedemptionByIdSpecification>(), Arg.Any<CancellationToken>())
            .Returns(redemption);

        var handler = new CancelRedemptionHandler(_redemptionRepo, _rewardRepo, _gameRepo, _unitOfWork);
        var result = await handler.HandleAsync(new CancelRedemptionCommand(redemption.Id.Value, Guid.NewGuid()), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task GetPlayerRedemptions_ReturnsOnlyOwnHistory()
    {
        var playerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var rewardId = new RewardId(Guid.NewGuid());
        var gameId = new GameId(Guid.NewGuid());
        var myRedemption = RewardRedemption.Create(playerId, rewardId, gameId, 100).Value;
        var otherRedemption = RewardRedemption.Create(otherId, rewardId, gameId, 50).Value;

        _redemptionRepo.ListAsync(Arg.Any<RedemptionsByPlayerSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new List<RewardRedemption> { myRedemption });

        var handler = new GetPlayerRedemptionsHandler(_redemptionRepo);
        var result = await handler.HandleAsync(new GetPlayerRedemptionsQuery(playerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Redemptions);
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
            game.AdjustPoints(playerId, initialPoints, "Test setup", Guid.NewGuid());

        return game;
    }

    private static RewardRedemption CreateRedemption(RedemptionStatus status)
    {
        var playerId = Guid.NewGuid();
        var redemption = RewardRedemption.Create(playerId, new RewardId(Guid.NewGuid()), new GameId(Guid.NewGuid()), 100).Value;

        if (status == RedemptionStatus.Approved)
            redemption.Approve(Guid.NewGuid());
        else if (status == RedemptionStatus.Rejected)
            redemption.Reject(Guid.NewGuid());
        else if (status == RedemptionStatus.Delivered)
        {
            redemption.Approve(Guid.NewGuid());
            redemption.Deliver(Guid.NewGuid());
        }
        else if (status == RedemptionStatus.Cancelled)
            redemption.Cancel(playerId);

        return redemption;
    }
}
