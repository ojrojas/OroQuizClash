using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringLedgerTests
{
    [Fact]
    public void Ledger_SumEqualsCurrentPoints_AfterMixedOperations()
    {
        var config = ScoringTestBase.Config(lossPolicy: LossPolicy.LoseCurrentRound);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);

        game.AwardPoints(player, 100, PointTransactionType.AnswerCorrect, roundScoped: true);
        game.SecurePoints(player);
        game.AwardPoints(player, 50, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 75, PointTransactionType.AnswerCorrect, roundScoped: true);
        game.RemovePoints(player, PointTransactionType.AnswerIncorrect);
        game.AwardPoints(player, 25, PointTransactionType.LevelBonus, roundScoped: false);
        game.ConsumePoints(player, 30, "Redemption");

        var ledgerSum = game.PointTransactions.Where(pt => pt.PlayerId == player).Sum(pt => pt.Points);
        var score = game.GetPlayerScore(player);

        Assert.Equal(score.CurrentPoints, ledgerSum);
    }

    [Fact]
    public void Ledger_ResultingBalance_MatchesRunningSum()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 100, PointTransactionType.AnswerCorrect, roundScoped: true);
        game.AwardPoints(player, 50, PointTransactionType.RoundBonus, roundScoped: false);
        game.ConsumePoints(player, 30, "Redemption");

        var transactions = game.PointTransactions
            .Where(pt => pt.PlayerId == player)
            .OrderBy(pt => pt.CreatedAt)
            .ToList();

        var runningSum = 0;
        foreach (var t in transactions)
        {
            runningSum += t.Points;
            Assert.Equal(runningSum, t.ResultingBalance);
        }
    }

    [Fact]
    public void Ledger_EveryOperation_CreatesExactlyOneTransaction()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        game.AwardPoints(player, 100, PointTransactionType.AnswerCorrect, roundScoped: true);
        Assert.Single(game.PointTransactions);

        game.SecurePoints(player);
        Assert.Single(game.PointTransactions);

        game.AwardPoints(player, 50, PointTransactionType.RoundBonus, roundScoped: false);
        Assert.Equal(2, game.PointTransactions.Count);
    }

    [Fact]
    public void Ledger_TransactionsContainFullTraceability()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        var round = ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));

        var t = game.PointTransactions.Single(pt => pt.PlayerId == player);

        Assert.Equal(game.Id, t.GameId);
        Assert.Equal(player, t.PlayerId);
        Assert.Equal(round.Id, t.RoundId);
        Assert.Equal(question.Id, t.QuestionId);
        Assert.NotNull(t.AnswerId);
        Assert.True(t.CreatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void GetScore_LedgerReconstruction_MatchesPlayerScore()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 200, PointTransactionType.RoundBonus, roundScoped: false);
        game.ConsumePoints(player, 50, "Redemption");

        Assert.Equal(150, game.GetScore(player));
        Assert.Equal(150, game.GetPlayerScore(player).CurrentPoints);
    }
}
