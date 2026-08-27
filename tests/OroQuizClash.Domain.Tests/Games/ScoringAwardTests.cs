using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringAwardTests
{
    [Fact]
    public void SubmitAnswer_Correct_AwardsPointsViaLedger()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 1);

        var result = game.SubmitAnswer(
            player1,
            ScoringTestBase.CorrectOption(question),
            DateTimeOffset.UtcNow,
            ScoringTestBase.Resolver(question));

        Assert.True(result.IsSuccess);

        var transaction = game.PointTransactions.Single(pt => pt.PlayerId == player1);
        Assert.Equal(PointTransactionType.AnswerCorrect, transaction.Type);
        Assert.Equal(100, transaction.Points);
        Assert.Equal(100, transaction.ResultingBalance);

        var score = game.GetPlayerScore(player1);
        Assert.Equal(100, score.CurrentPoints);
        Assert.Equal(100, score.RoundPoints);
        Assert.Equal(0, score.SecuredPoints);
        Assert.Equal(100, score.TotalPoints);
    }

    [Fact]
    public void SubmitAnswer_Correct_AppliesDifficultyMultiplier()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 3);

        game.SubmitAnswer(
            player1,
            ScoringTestBase.CorrectOption(question),
            DateTimeOffset.UtcNow,
            ScoringTestBase.Resolver(question));

        var transaction = game.PointTransactions.Single(pt => pt.PlayerId == player1);
        Assert.Equal(150, transaction.Points);
        Assert.Equal(150, transaction.ResultingBalance);
    }

    [Fact]
    public void AwardPoints_ValidState_CreatesTransactionAndRaisesEvent()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);

        var result = game.AwardPoints(player1, 50, PointTransactionType.RoundBonus);

        Assert.True(result.IsSuccess);
        Assert.Equal(50, result.Value.Points);
        Assert.Equal(50, result.Value.ResultingBalance);
        Assert.Contains(game.DomainEvents, e => e is ScoreUpdatedDomainEvent sue && sue.PlayerId == player1 && sue.Points == 50);
    }

    [Fact]
    public void AwardPoints_TerminalState_FailsInvalidScoringState()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        game.Cancel("Test cancellation");

        var result = game.AwardPoints(player1, 50, PointTransactionType.RoundBonus);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidScoringState.Code, result.Error.Code);
    }

    [Fact]
    public void AwardPoints_UnknownPlayer_FailsPlayerNotInGame()
    {
        var game = ScoringTestBase.CreateStartedGame(out _, out _);

        var result = game.AwardPoints(Guid.NewGuid(), 50, PointTransactionType.RoundBonus);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerNotInGame.Code, result.Error.Code);
    }

    [Fact]
    public void AwardPoints_ZeroAmount_FailsInvalidAmount()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);

        var result = game.AwardPoints(player1, 0, PointTransactionType.RoundBonus);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidAdjustmentAmount.Code, result.Error.Code);
    }

    [Fact]
    public void StartRound_SetsPotentialPoints_AndResetsRoundPoints()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);

        ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 2);

        var score = game.GetPlayerScore(player1);
        Assert.Equal(125, score.PotentialPoints);
        Assert.Equal(0, score.RoundPoints);
    }

    [Fact]
    public void SubmitAnswer_Correct_ResultingBalanceTracksRunningTotal()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q1, difficulty: 1);
        game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q1), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q1));
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q2, difficulty: 1);
        game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q2), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q2));

        var transactions = game.PointTransactions
            .Where(pt => pt.PlayerId == player1 && pt.Type == PointTransactionType.AnswerCorrect)
            .OrderBy(pt => pt.CreatedAt)
            .ToList();

        Assert.Equal(2, transactions.Count);
        Assert.Equal(100, transactions[0].ResultingBalance);
        Assert.Equal(200, transactions[1].ResultingBalance);
    }
}
