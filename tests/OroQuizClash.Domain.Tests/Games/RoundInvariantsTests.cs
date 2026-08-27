using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class RoundInvariantsTests
{
    private static GameConfiguration ValidConfig(int minRounds = 5)
    {
        return new GameConfiguration(
            "Quiz Masters",
            new CategoryId(Guid.NewGuid()),
            minRounds, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10);
    }

    [Fact]
    public void Create_WithMinRoundsLessThanFive_FailsMinRoundsTooLow()
    {
        var result = Domain.Games.Game.Create(ValidConfig(minRounds: 4), Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.MinRoundsTooLow.Code, result.Error.Code);
    }

    [Fact]
    public void Finish_WithThreeCompletedRounds_FailsNotEnoughRounds()
    {
        var game = Domain.Games.Game.Create(ValidConfig(minRounds: 5), Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        game.JoinPlayer(Guid.NewGuid(), "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();

        for (int i = 0; i < 3; i++)
        {
            var round = game.StartRound(Guid.NewGuid(), 1).Value;
            game.CompleteRound(round.Id.Value);
        }

        var result = game.Finish();

        Assert.True(result.IsFailure);
        Assert.Contains("Not enough rounds", result.Error.Description);
    }
}
