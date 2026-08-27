using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class RoundSelectionTests
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

    private static Domain.Games.Game CreateGameInProgress()
    {
        var game = Domain.Games.Game.Create(ValidConfig(), Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        game.JoinPlayer(Guid.NewGuid(), "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        return game;
    }

    [Fact]
    public void StartRound_ExcludesPreviouslyUsedQuestions()
    {
        var game = CreateGameInProgress();
        var qId1 = Guid.NewGuid();
        var qId2 = Guid.NewGuid();

        var round1 = game.StartRound(qId1, 1).Value;
        game.CompleteRound(round1.Id.Value);

        // Try same question again after completing the round
        var result = game.StartRound(qId1, 2);

        Assert.True(result.IsFailure);
        Assert.Contains("already used", result.Error.Description);
    }

    [Fact]
    public void StartRound_WithEmptyQuestionId_FailsNoAvailableQuestion()
    {
        var game = CreateGameInProgress();

        var result = game.StartRound(Guid.Empty, 1);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.NoAvailableQuestion.Code, result.Error.Code);
    }
}
