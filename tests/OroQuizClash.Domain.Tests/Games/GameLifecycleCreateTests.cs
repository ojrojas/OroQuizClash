using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class GameLifecycleCreateTests
{
    private static GameConfiguration ValidConfig(int minPlayers = 2, int maxPlayers = 10)
    {
        return new GameConfiguration(
            "Quiz Masters",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            minPlayers, maxPlayers);
    }

    private static Domain.Games.Game CreateGame(int minPlayers = 2, int maxPlayers = 10)
    {
        var result = Domain.Games.Game.Create(ValidConfig(minPlayers, maxPlayers), Guid.NewGuid());
        return result.Value;
    }

    [Fact]
    public void MarkReady_WithFiveValidQuestions_SucceedsReady()
    {
        var game = CreateGame();

        var result = game.MarkReady(
            _ => true,
            _ => 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.Ready, game.Status);
        Assert.NotNull(game.ReadyAt);
        Assert.Contains(game.DomainEvents, e => e is GameReadyDomainEvent);
    }

    [Fact]
    public void MarkReady_WithLessThanFive_FailsCategoryNotReady()
    {
        var game = CreateGame();

        var result = game.MarkReady(
            _ => true,
            _ => 4);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.CategoryNotReady.Code, result.Error.Code);
    }

    [Fact]
    public void OpenLobby_FromReady_SucceedsWaitingForPlayers()
    {
        var game = CreateGame();
        game.MarkReady(_ => true, _ => 5);

        var result = game.OpenLobby();

        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.WaitingForPlayers, game.Status);
    }

    [Fact]
    public void JoinPlayer_SucceedsAndRaisesPlayerJoined()
    {
        var game = CreateGame();
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        var userId = Guid.NewGuid();

        var result = game.JoinPlayer(userId, "Alice");

        Assert.True(result.IsSuccess);
        Assert.Single(game.Players);
        Assert.Equal(userId, game.Players[0].UserId);
        Assert.Contains(game.DomainEvents, e => e is PlayerJoinedDomainEvent);
    }

    [Fact]
    public void JoinPlayer_Duplicate_FailsPlayerAlreadyJoined()
    {
        var game = CreateGame();
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        var userId = Guid.NewGuid();
        game.JoinPlayer(userId, "Alice");

        var result = game.JoinPlayer(userId, "Alice");

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerAlreadyJoined.Code, result.Error.Code);
    }
}
