using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ConsolidationFinishTests
{
    private static void PlayRoundWithBothPlayers(Domain.Games.Game game, Guid winner, Guid loser)
    {
        var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q, difficulty: 1);
        game.SubmitAnswer(winner, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
        game.SubmitAnswer(loser, ScoringTestBase.IncorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
        game.CompleteRound(game.CurrentRound!.Id.Value);
    }

    [Fact]
    public void Finish_FixedPoints_AwardsToEligibleNonWinner()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            lossPolicy: LossPolicy.LoseCurrentRound,
            pointsPerRound: 100,
            minRounds: 5,
            consolationPoints: 50);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);

        for (int i = 0; i < 5; i++)
            PlayRoundWithBothPlayers(game, winner, loser);

        game.Finish();

        var consolation = game.PointTransactions
            .Where(pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == loser)
            .ToList();
        Assert.Single(consolation);
        Assert.Equal(50, consolation[0].Points);
    }

    [Fact]
    public void Finish_ParticipationBased_AwardsScaledPoints()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.ParticipationBased,
            lossPolicy: LossPolicy.LoseCurrentRound,
            pointsPerRound: 100,
            minRounds: 5,
            consolationPoints: 100);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);

        for (int i = 0; i < 5; i++)
            PlayRoundWithBothPlayers(game, winner, loser);

        game.Finish();

        var consolation = game.PointTransactions
            .Where(pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == loser)
            .ToList();
        Assert.Single(consolation);
        Assert.Equal(100, consolation[0].Points);
    }

    [Fact]
    public void Finish_WinnerDeterminedBeforeConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            lossPolicy: LossPolicy.LoseCurrentRound,
            pointsPerRound: 100,
            minRounds: 5,
            consolationPoints: 1000);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);

        for (int i = 0; i < 5; i++)
            PlayRoundWithBothPlayers(game, winner, loser);

        game.Finish();

        var winnerPlayer = game.Players.First(p => p.UserId == winner);
        Assert.Equal(PlayerParticipationStatus.Winner, winnerPlayer.ParticipationStatus);
        Assert.DoesNotContain(game.PointTransactions,
            pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == winner);
    }

    [Fact]
    public void Finish_EliminatedPlayer_NoConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            lossPolicy: LossPolicy.LoseCurrentRound,
            pointsPerRound: 100,
            minRounds: 5,
            consolationPoints: 100);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var eliminated, config);

        for (int i = 0; i < 3; i++)
            PlayRoundWithBothPlayers(game, winner, eliminated);

        game.EliminatePlayer(eliminated, "test");

        for (int i = 3; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q, difficulty: 1);
            game.SubmitAnswer(winner, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }

        game.Finish();

        Assert.DoesNotContain(game.PointTransactions,
            pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == eliminated);
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

    [Fact]
    public void Finish_WithdrawnPlayer_BelowThresholds_NoConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore,
            pointsPerRound: 100,
            minRounds: 5,
            consolationPoints: 50,
            minimumParticipationRounds: 3);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var withdrawn, config);

        PlayRoundWithBothPlayers(game, winner, withdrawn);

        game.WithdrawPlayer(withdrawn);

        for (int i = 1; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q, difficulty: 1);
            game.SubmitAnswer(winner, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }

        game.Finish();

        Assert.DoesNotContain(game.PointTransactions,
            pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == withdrawn);
    }

    [Fact]
    public void Finish_NoDoubleConsolation()
    {
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.FixedPoints,
            lossPolicy: LossPolicy.LoseCurrentRound,
            pointsPerRound: 100,
            minRounds: 5,
            consolationPoints: 100);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);

        for (int i = 0; i < 5; i++)
            PlayRoundWithBothPlayers(game, winner, loser);

        game.Finish();

        var consolation = game.PointTransactions
            .Where(pt => pt.Type == PointTransactionType.Consolation && pt.PlayerId == loser)
            .ToList();
        Assert.Single(consolation);
    }

    [Fact]
    public void Finish_RewardBased_CreatesApprovedRedemption()
    {
        var rewardId = Guid.NewGuid();
        var config = ScoringTestBase.Config(
            consolationPolicy: ConsolationPolicy.RewardBased,
            lossPolicy: LossPolicy.LoseCurrentRound,
            pointsPerRound: 100,
            minRounds: 5,
            consolationRewardId: rewardId);
        var game = ScoringTestBase.CreateStartedGame(out var winner, out var loser, config);

        for (int i = 0; i < 5; i++)
            PlayRoundWithBothPlayers(game, winner, loser);

        game.Finish();

        Assert.Empty(game.PointTransactions.Where(pt => pt.Type == PointTransactionType.Consolation));
        Assert.NotNull(game.ConsolationRedemptions);
        Assert.Single(game.ConsolationRedemptions);
        Assert.Equal(loser, game.ConsolationRedemptions[0].PlayerId);
        Assert.Equal(RedemptionStatus.Approved, game.ConsolationRedemptions[0].Status);
    }
}
