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

    private static void PlayRound(Domain.Games.Game game, Guid player, bool correct)
    {
        var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q, difficulty: 1);
        var option = correct ? ScoringTestBase.CorrectOption(q) : ScoringTestBase.IncorrectOption(q);
        game.SubmitAnswer(player, option, DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
        game.CompleteRound(game.CurrentRound!.Id.Value);
    }

    private static void PlayRoundWithBothPlayers(Domain.Games.Game game, Guid winner, Guid loser)
    {
        var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q, difficulty: 1);
        game.SubmitAnswer(winner, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
        game.SubmitAnswer(loser, ScoringTestBase.IncorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
        game.CompleteRound(game.CurrentRound!.Id.Value);
    }

    [Fact]
    public void Finish_FixedPointsPolicy_EligibleNonWinner_ReceivesConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            lossPolicy: LossPolicy.LoseCurrentRound,
            pointsPerRound: 100,
            minRounds: 5,
            consolationPoints: 100);
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
            minRounds: 5,
            consolationPoints: 100);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);
        PlayRounds(game, winner, 5, correct: true);
        PlayRounds(game, loser, 5, correct: true);

        game.Finish();

        var consolation = game.PointTransactions.Where(pt => pt.Type == PointTransactionType.Consolation).ToList();
        Assert.DoesNotContain(consolation, c => c.PlayerId == winner);
    }

    [Fact]
    public void Finish_WithdrawnPlayer_MeetsThresholds_ReceivesConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore,
            pointsPerRound: 100,
            minRounds: 5,
            consolationPoints: 50);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var withdrawn, config);

        for (int i = 0; i < 3; i++)
            PlayRoundWithBothPlayers(game, winner, withdrawn);

        game.WithdrawPlayer(withdrawn);

        for (int i = 3; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q, difficulty: 1);
            game.SubmitAnswer(winner, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }

        game.Finish();

        var consolation = game.PointTransactions
            .Where(pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == withdrawn)
            .ToList();
        Assert.Single(consolation);
        Assert.Equal(50, consolation[0].Points);
    }
}
