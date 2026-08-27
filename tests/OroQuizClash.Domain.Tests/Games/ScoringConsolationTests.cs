using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringConsolationTests
{
    private static void PlayRounds(Domain.Games.Game game, Guid player, int rounds, bool correct = true)
    {
        for (var i = 0; i < rounds; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q, difficulty: 1);
            var option = correct ? ScoringTestBase.CorrectOption(q) : ScoringTestBase.IncorrectOption(q);
            game.SubmitAnswer(player, option, DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }
    }

    [Fact]
    public void Finish_FixedPointsPolicy_EligibleNonWinner_ReceivesConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            lossPolicy: LossPolicy.LoseCurrentRound,
            pointsPerRound: 100,
            minRounds: 5);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);
        PlayRounds(game, winner, 5, correct: true);
        PlayRounds(game, loser, 5, correct: false);

        game.Finish();

        var consolation = game.PointTransactions
            .Where(pt => pt.Type == PointTransactionType.Consolation)
            .ToList();

        Assert.Single(consolation);
        Assert.Equal(loser, consolation[0].PlayerId);
        Assert.Equal(100, consolation[0].Points);
    }

    [Fact]
    public void Finish_NonePolicy_NoConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.None,
            lossPolicy: LossPolicy.LoseCurrentRound,
            minRounds: 5);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);
        PlayRounds(game, winner, 5, correct: true);
        PlayRounds(game, loser, 5, correct: false);

        game.Finish();

        Assert.DoesNotContain(game.PointTransactions, pt => pt.Type == PointTransactionType.Consolation);
    }

    [Fact]
    public void Finish_Winner_NoConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            pointsPerRound: 100,
            minRounds: 5);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);
        PlayRounds(game, winner, 5, correct: true);
        PlayRounds(game, loser, 5, correct: true);

        game.Finish();

        var consolation = game.PointTransactions.Where(pt => pt.Type == PointTransactionType.Consolation).ToList();
        Assert.DoesNotContain(consolation, c => c.PlayerId == winner);
    }

    [Fact]
    public void Finish_WithdrawnPlayer_NoConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore,
            pointsPerRound: 100,
            minRounds: 5);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var withdrawn, config);
        PlayRounds(game, winner, 5, correct: true);
        PlayRounds(game, withdrawn, 5, correct: false);
        game.WithdrawPlayer(withdrawn);

        game.Finish();

        Assert.DoesNotContain(game.PointTransactions,
            pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == withdrawn);
    }
}
