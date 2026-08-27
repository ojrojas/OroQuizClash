using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringGameBonusTests
{
    private static Domain.Games.Game PlayMinimumRounds(Domain.Games.Game game, Guid player, int rounds)
    {
        for (var i = 0; i < rounds; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q, difficulty: 1);
            game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }
        return game;
    }

    [Fact]
    public void Finish_AwardsGameBonus_ToActivePlayers()
    {
        var config = ScoringTestBase.Config(pointsPerRound: 100, minRounds: 5);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        PlayMinimumRounds(game, player1, 5);
        PlayMinimumRounds(game, player2, 5);

        game.Finish();

        var bonuses = game.PointTransactions.Where(pt => pt.Type == PointTransactionType.GameBonus).ToList();
        Assert.Equal(2, bonuses.Count);
        Assert.All(bonuses, b => Assert.Equal(100, b.Points));
    }

    [Fact]
    public void Finish_WithdrawnPlayer_NoGameBonus()
    {
        var config = ScoringTestBase.Config(pointsPerRound: 100, minRounds: 5, withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        PlayMinimumRounds(game, player1, 5);
        PlayMinimumRounds(game, player2, 5);
        game.WithdrawPlayer(player2);

        game.Finish();

        var bonuses = game.PointTransactions.Where(pt => pt.Type == PointTransactionType.GameBonus).ToList();
        Assert.Single(bonuses);
        Assert.Equal(player1, bonuses[0].PlayerId);
    }

    [Fact]
    public void Finish_GameBonus_IncludedInFinalBalance()
    {
        var config = ScoringTestBase.Config(pointsPerRound: 100, minRounds: 5);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _, config);
        PlayMinimumRounds(game, player1, 5);
        var scoreBeforeFinish = game.GetPlayerScore(player1).CurrentPoints;

        game.Finish();

        var scoreAfterFinish = game.GetPlayerScore(player1);
        Assert.Equal(scoreBeforeFinish + 100, scoreAfterFinish.CurrentPoints);
        Assert.Equal(scoreAfterFinish.CurrentPoints, game.PointTransactions.Where(pt => pt.PlayerId == player1).Sum(pt => pt.Points));
    }
}
