using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class WithdrawalImpactTests
{
    [Fact]
    public void WithdrawnPlayer_ExcludedFromRoundBonus()
    {
        var config = ScoringTestBase.Config(scoringSystem: ScoringSystem.ProgressiveBonus, withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question);
        game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));
        game.WithdrawPlayer(player2);

        game.CompleteRound(game.CurrentRound!.Id.Value);

        Assert.DoesNotContain(game.PointTransactions,
            pt => pt.PlayerId == player2 && pt.Type == PointTransactionType.RoundBonus);
        Assert.Contains(game.PointTransactions,
            pt => pt.PlayerId == player1 && pt.Type == PointTransactionType.RoundBonus);
    }

    [Fact]
    public void EliminatedPlayer_ExcludedFromRoundBonus()
    {
        var config = ScoringTestBase.Config(scoringSystem: ScoringSystem.ProgressiveBonus);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question);
        game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));
        game.EliminatePlayer(player2, "Eliminated");

        game.CompleteRound(game.CurrentRound!.Id.Value);

        Assert.DoesNotContain(game.PointTransactions,
            pt => pt.PlayerId == player2 && pt.Type == PointTransactionType.RoundBonus);
    }

    [Fact]
    public void WithdrawnPlayer_ExcludedFromGameBonus()
    {
        var config = ScoringTestBase.Config(minRounds: 5, withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        for (var i = 0; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q);
            game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }
        game.WithdrawPlayer(player2);

        game.Finish();

        Assert.DoesNotContain(game.PointTransactions,
            pt => pt.PlayerId == player2 && pt.Type == PointTransactionType.GameBonus);
        Assert.Contains(game.PointTransactions,
            pt => pt.PlayerId == player1 && pt.Type == PointTransactionType.GameBonus);
    }

    [Fact]
    public void EliminatedPlayer_ExcludedFromGameBonusAndConsolation()
    {
        var config = ScoringTestBase.Config(
            minRounds: 5,
            consolationPolicy: ConsolationPolicy.FixedPoints,
            lossPolicy: LossPolicy.LoseCurrentRound);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        for (var i = 0; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q);
            game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }
        game.EliminatePlayer(player2, "Eliminated");

        game.Finish();

        Assert.DoesNotContain(game.PointTransactions,
            pt => pt.PlayerId == player2 && (pt.Type == PointTransactionType.GameBonus || pt.Type == PointTransactionType.Consolation));
    }

    [Fact]
    public void WithdrawnPlayer_CannotReceiveAwards()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.WithdrawPlayer(player);

        var result = game.AwardPoints(player, 100, PointTransactionType.RoundBonus);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void EliminatedPlayer_CannotReceiveAwards()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.EliminatePlayer(player, "Eliminated");

        var result = game.AwardPoints(player, 100, PointTransactionType.RoundBonus);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void RemainingPlayers_ContinueScoring_AfterWithdrawal()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        game.WithdrawPlayer(player2);

        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q1);
        var result = game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q1), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q1));

        Assert.True(result.IsSuccess);
        Assert.Equal(100, game.GetPlayerScore(player1).CurrentPoints);
    }

    [Fact]
    public void MidRoundWithdrawal_Succeeds_RoundContinues()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question);

        var withdrawResult = game.WithdrawPlayer(player2);
        Assert.True(withdrawResult.IsSuccess);

        var answerResult = game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));
        Assert.True(answerResult.IsSuccess);

        Assert.DoesNotContain(game.Answers, a => a.PlayerId == player2);
    }

    [Fact]
    public void SingleActivePlayer_GameContinues()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore, minPlayers: 2);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        game.WithdrawPlayer(player2);

        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        var roundResult = game.StartRound(q1.Id.Value, 1);

        Assert.True(roundResult.IsSuccess);
        Assert.Equal(GameStatus.RoundInProgress, game.Status);
    }
}
