using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class GameRoundLifecycleTests
{
    private static GameConfiguration ValidConfig(int minRounds = 5, int minPlayers = 2, int maxPlayers = 10)
    {
        return new GameConfiguration(
            "Quiz Masters",
            new CategoryId(Guid.NewGuid()),
            minRounds, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            minPlayers, maxPlayers);
    }

    private static Domain.Games.Game CreateGameInWaiting(int minPlayers = 2)
    {
        var game = Domain.Games.Game.Create(ValidConfig(minPlayers: minPlayers), Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        return game;
    }

    private static QuestionId NewQuestionId() => new(Guid.NewGuid());

    [Fact]
    public void Start_FromWaitingWithTwoPlayers_SucceedsInProgress()
    {
        var game = CreateGameInWaiting();
        game.JoinPlayer(Guid.NewGuid(), "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");

        var result = game.Start();

        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.NotNull(game.StartedAt);
        Assert.Contains(game.DomainEvents, e => e is GameStartedDomainEvent);
    }

    [Fact]
    public void Start_WithOnePlayer_FailsNotEnoughPlayers()
    {
        var game = CreateGameInWaiting();
        game.JoinPlayer(Guid.NewGuid(), "Alice");

        var result = game.Start();

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.NotEnoughPlayers.Code, result.Error.Code);
    }

    [Fact]
    public void StartRound_FromInProgress_SucceedsRoundInProgress()
    {
        var game = CreateGameInWaiting();
        game.JoinPlayer(Guid.NewGuid(), "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        var qId = NewQuestionId();

        var result = game.StartRound(qId.Value, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.RoundInProgress, game.Status);
        Assert.Single(game.Rounds);
        Assert.Contains(game.DomainEvents, e => e is RoundStartedDomainEvent);
    }

    [Fact]
    public void CompleteRound_FromRoundInProgress_SucceedsRoundCompleted()
    {
        var game = CreateGameInWaiting();
        game.JoinPlayer(Guid.NewGuid(), "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        var round = game.StartRound(NewQuestionId().Value, 1).Value;

        var result = game.CompleteRound(round.Id.Value);

        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.RoundCompleted, game.Status);
        Assert.Contains(game.DomainEvents, e => e is RoundCompletedDomainEvent);
    }

    [Fact]
    public void Finish_FromRoundCompleted_SucceedsFinished()
    {
        var game = CreateGameInWaiting(minPlayers: 2);
        game.JoinPlayer(Guid.NewGuid(), "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();

        // Play 5 rounds to satisfy MinRounds=5
        for (int i = 0; i < 5; i++)
        {
            var round = game.StartRound(NewQuestionId().Value, 1).Value;
            game.CompleteRound(round.Id.Value);
        }

        var result = game.Finish();

        Assert.True(result.IsSuccess);
        Assert.Equal(GameStatus.Finished, game.Status);
        Assert.NotNull(game.FinishedAt);
        Assert.Contains(game.DomainEvents, e => e is GameFinishedDomainEvent);
    }
}
