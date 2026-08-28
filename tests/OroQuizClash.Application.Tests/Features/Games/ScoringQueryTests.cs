using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Specifications;

using NSubstitute;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class ScoringQueryTests
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

    private static IRepository<Game, GameId> RepoReturning(Game? game)
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(game));
        return repo;
    }

    [Fact]
    public async Task GetPlayerScore_GameNotFound_Fails()
    {
        var handler = new GetPlayerScoreHandler(RepoReturning(null));

        var result = await handler.HandleAsync(new GetPlayerScoreQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("GameNotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetPlayerScore_PlayerNotInGame_Fails()
    {
        var game = CreateGameWithPlayer(out _);
        var handler = new GetPlayerScoreHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetPlayerScoreQuery(game.Id.Value, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlayerNotInGame", result.Error.Code);
    }

    [Fact]
    public async Task GetPlayerScore_ReturnsFullBreakdown()
    {
        var game = CreateGameWithPlayer(out var playerId);
        game.AwardPoints(playerId, 100, PointTransactionType.AnswerCorrect, roundScoped: true);
        game.SecurePoints(playerId);
        game.AwardPoints(playerId, 50, PointTransactionType.RoundBonus, roundScoped: false);
        var handler = new GetPlayerScoreHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetPlayerScoreQuery(game.Id.Value, playerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(150, result.Value.CurrentPoints);
        Assert.Equal(150, result.Value.SecuredPoints);
        Assert.Equal(0, result.Value.RoundPoints);
        Assert.Equal(150, result.Value.TotalPoints);
        Assert.Equal(1, result.Value.CorrectAnswers);
        Assert.False(result.Value.IsWithdrawn);
    }

    [Fact]
    public async Task GetScoreLedger_ReturnsPaginatedTransactions()
    {
        var game = CreateGameWithPlayer(out var playerId);
        for (var i = 0; i < 5; i++)
            game.AwardPoints(playerId, 10, PointTransactionType.RoundBonus, roundScoped: false);
        var handler = new GetScoreLedgerHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetScoreLedgerQuery(game.Id.Value, playerId, Page: 1, PageSize: 2), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.True(result.Value.HasMore);
    }

    [Fact]
    public async Task GetScoreLedger_FilterByType_ReturnsOnlyMatching()
    {
        var game = CreateGameWithPlayer(out var playerId);
        game.AwardPoints(playerId, 10, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(playerId, 20, PointTransactionType.LevelBonus, roundScoped: false);
        var handler = new GetScoreLedgerHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetScoreLedgerQuery(game.Id.Value, playerId, Type: "LEVEL_BONUS"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal("LEVEL_BONUS", result.Value.Items[0].Type);
    }

    [Fact]
    public async Task GetScoreLedger_UnknownType_Fails()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var handler = new GetScoreLedgerHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetScoreLedgerQuery(game.Id.Value, playerId, Type: "INVALID_TYPE"), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GetLeaderboard_RanksByCurrentPointsDescending()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var otherPlayer = game.Players.First(p => p.UserId != playerId).UserId;
        game.AwardPoints(playerId, 100, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(otherPlayer, 250, PointTransactionType.RoundBonus, roundScoped: false);
        var handler = new GetLeaderboardHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Players.Count);
        Assert.Equal(otherPlayer, result.Value.Players[0].PlayerId);
        Assert.Equal(1, result.Value.Players[0].Rank);
        Assert.Equal(250, result.Value.Players[0].Points);
        Assert.Equal(playerId, result.Value.Players[1].PlayerId);
    }

    [Fact]
    public async Task WithdrawPlayer_Handler_Succeeds()
    {
        var game = CreateGameWithPlayer(out var playerId);
        game.AwardPoints(playerId, 100, PointTransactionType.RoundBonus, roundScoped: false);
        var repo = RepoReturning(game);
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new WithdrawPlayerHandler(repo, uow);

        var result = await handler.HandleAsync(new WithdrawPlayerCommand(game.Id.Value, playerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.FinalScore);
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
