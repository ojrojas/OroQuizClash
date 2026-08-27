using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Specifications;

using NSubstitute;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class AdjustScoreTests
{
    private static Game CreateGameWithPlayer(out Guid playerId)
    {
        var config = new GameConfiguration(
            "Scoring Game",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10, 100);

        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        playerId = Guid.NewGuid();
        game.JoinPlayer(playerId, "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        return game;
    }

    private static (AdjustScoreHandler handler, IUnitOfWork uow) HandlerFor(Game? game)
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(game));
        var uow = Substitute.For<IUnitOfWork>();
        return (new AdjustScoreHandler(repo, uow), uow);
    }

    [Fact]
    public async Task AdjustScore_ValidCommand_Succeeds()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var (handler, uow) = HandlerFor(game);
        var cmd = new AdjustScoreCommand(game.Id.Value, playerId, 100, "System error correction", Guid.NewGuid());

        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.Points);
        Assert.Equal(100, result.Value.ResultingBalance);
        Assert.Equal("System error correction", result.Value.Reason);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdjustScore_GameNotFound_Fails()
    {
        var (handler, _) = HandlerFor(null);
        var cmd = new AdjustScoreCommand(Guid.NewGuid(), Guid.NewGuid(), 100, "Valid reason here", Guid.NewGuid());

        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("GameNotFound", result.Error.Code);
    }

    [Fact]
    public async Task AdjustScore_InvalidReason_FailsAtDomain()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var (handler, uow) = HandlerFor(game);
        var cmd = new AdjustScoreCommand(game.Id.Value, playerId, 100, "ab", Guid.NewGuid());

        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AdjustmentReasonRequired", result.Error.Code);
        await uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdjustScore_ZeroPoints_FailsAtDomain()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var (handler, _) = HandlerFor(game);
        var cmd = new AdjustScoreCommand(game.Id.Value, playerId, 0, "Valid reason here", Guid.NewGuid());

        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InvalidAdjustmentAmount", result.Error.Code);
    }

    [Fact]
    public async Task AdjustScore_Validator_RejectsInvalidInput()
    {
        var validator = new AdjustScoreValidator();

        var failures = await validator.ValidateAsync(
            new AdjustScoreCommand(Guid.Empty, Guid.Empty, 0, "", Guid.Empty),
            CancellationToken.None);

        Assert.Equal(4, failures.Count);
    }

    [Fact]
    public async Task AdjustScore_NegativeExceedingBalance_Fails()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var (handler, _) = HandlerFor(game);
        var cmd = new AdjustScoreCommand(game.Id.Value, playerId, -500, "Valid reason here", Guid.NewGuid());

        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("InsufficientPoints", result.Error.Code);
    }
}
