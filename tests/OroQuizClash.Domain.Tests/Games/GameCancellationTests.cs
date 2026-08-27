using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class GameCancellationTests
{
    private static GameConfiguration ValidConfig()
    {
        return new GameConfiguration(
            "Quiz Masters",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10);
    }

    private static Domain.Games.Game CreateGame()
    {
        return Domain.Games.Game.Create(ValidConfig(), Guid.NewGuid()).Value;
    }

    [Fact]
    public void Cancel_FromDraft_SucceedsCancelled()
    {
        var game = CreateGame();

        var result = game.Cancel("Player dropped out");

        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.Cancelled, game.Status);
        Assert.NotNull(game.FinishedAt);
        Assert.Contains(game.DomainEvents, e => e is GameCancelledDomainEvent);
    }

    [Fact]
    public void Cancel_FromFinished_FailsInvalidGameState()
    {
        var game = CreateGame();
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        game.JoinPlayer(Guid.NewGuid(), "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        for (int i = 0; i < 5; i++)
        {
            var round = game.StartRound(Guid.NewGuid(), 1).Value;
            game.CompleteRound(round.Id.Value);
        }
        game.Finish();

        var result = game.Cancel("Too late");

        Assert.True(result.IsFailure);
        Assert.Contains("InvalidGameState", result.Error.Code);
    }

    [Fact]
    public void ForceFinish_FromInProgress_SucceedsForcedFinished()
    {
        var game = CreateGame();
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        game.JoinPlayer(Guid.NewGuid(), "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();

        var result = game.ForceFinish("Server maintenance");

        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.ForcedFinished, game.Status);
        Assert.NotNull(game.FinishedAt);
        Assert.Contains(game.DomainEvents, e => e is GameForcedFinishedDomainEvent);
    }
}
