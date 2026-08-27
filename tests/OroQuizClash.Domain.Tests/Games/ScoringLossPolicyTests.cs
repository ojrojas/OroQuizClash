using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringLossPolicyTests
{
    private static (Domain.Games.Game game, Guid player, Domain.Questions.Question question) SetupGame(LossPolicy policy)
    {
        var config = ScoringTestBase.Config(lossPolicy: policy, pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 1);

        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q2, difficulty: 1);
        return (game, player, q2);
    }

    [Fact]
    public void IncorrectAnswer_LoseAll_DeductsEverything()
    {
        var (game, player, question) = SetupGame(LossPolicy.LoseAll);

        game.SubmitAnswer(player, ScoringTestBase.IncorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));

        var score = game.GetPlayerScore(player);
        Assert.Equal(0, score.CurrentPoints);
        Assert.Equal(0, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);

        var transaction = game.PointTransactions.Last(pt => pt.PlayerId == player);
        Assert.Equal(PointTransactionType.AnswerIncorrect, transaction.Type);
        Assert.Equal(-100, transaction.Points);
        Assert.Equal(0, transaction.ResultingBalance);
    }

    [Fact]
    public void IncorrectAnswer_LoseCurrentRound_KeepsSecuredPoints()
    {
        var (game, player, question) = SetupGame(LossPolicy.LoseCurrentRound);

        game.SubmitAnswer(player, ScoringTestBase.IncorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));

        var score = game.GetPlayerScore(player);
        Assert.Equal(100, score.CurrentPoints);
        Assert.Equal(100, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);
    }

    [Fact]
    public void IncorrectAnswer_LoseUnsecuredPoints_KeepsSecuredPoints()
    {
        var (game, player, question) = SetupGame(LossPolicy.LoseUnsecuredPoints);

        game.SubmitAnswer(player, ScoringTestBase.IncorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));

        var score = game.GetPlayerScore(player);
        Assert.Equal(100, score.CurrentPoints);
        Assert.Equal(100, score.SecuredPoints);
    }

    [Fact]
    public void IncorrectAnswer_FallbackToCheckpoint_FallsBackToSecured()
    {
        var (game, player, question) = SetupGame(LossPolicy.FallbackToCheckpoint);

        game.SubmitAnswer(player, ScoringTestBase.IncorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));

        var score = game.GetPlayerScore(player);
        Assert.Equal(100, score.CurrentPoints);
        Assert.Equal(100, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);
    }

    [Fact]
    public void IncorrectAnswer_LoseCurrentRound_WithUnsecuredRoundPoints_DeductsOnlyRoundPoints()
    {
        var config = ScoringTestBase.Config(lossPolicy: LossPolicy.LoseCurrentRound, pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q1, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q1), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q1));
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q2, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q2), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q2));

        var q3 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        game.CompleteRound(game.CurrentRound!.Id.Value);
        ScoringTestBase.StartRoundWithQuestion(game, q3, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.IncorrectOption(q3), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q3));

        var score = game.GetPlayerScore(player);
        Assert.Equal(200, score.CurrentPoints);
        Assert.Equal(200, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);
    }

    [Fact]
    public void IncorrectAnswer_ZeroBalance_NoNegativeBalance()
    {
        var config = ScoringTestBase.Config(lossPolicy: LossPolicy.LoseAll, pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 1);

        game.SubmitAnswer(player, ScoringTestBase.IncorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));

        var score = game.GetPlayerScore(player);
        Assert.Equal(0, score.CurrentPoints);

        var transaction = game.PointTransactions.Single(pt => pt.PlayerId == player);
        Assert.Equal(0, transaction.Points);
        Assert.Equal(0, transaction.ResultingBalance);
    }

    [Fact]
    public void RemovePoints_TerminalState_FailsInvalidScoringState()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.Cancel("Test cancellation");

        var result = game.RemovePoints(player, PointTransactionType.Penalty);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidScoringState.Code, result.Error.Code);
    }

    [Fact]
    public void RemovePoints_LoseCurrentRound_WithUnsecuredRoundPoints_DeductsOnlyRoundPoints()
    {
        var config = ScoringTestBase.Config(lossPolicy: LossPolicy.LoseCurrentRound);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 50, PointTransactionType.AnswerCorrect, roundScoped: true);
        game.AwardPoints(player, 100, PointTransactionType.RoundBonus, roundScoped: false);

        var result = game.RemovePoints(player, PointTransactionType.AnswerIncorrect);

        Assert.True(result.IsSuccess);
        var score = game.GetPlayerScore(player);
        Assert.Equal(100, score.CurrentPoints);
        Assert.Equal(100, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);
        Assert.Equal(-50, result.Value.Points);
    }

    [Fact]
    public void RemovePoints_LoseUnsecuredPoints_DeductsUnsecuredOnly()
    {
        var config = ScoringTestBase.Config(lossPolicy: LossPolicy.LoseUnsecuredPoints);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 100, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 75, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.RemovePoints(player, PointTransactionType.AnswerIncorrect);

        Assert.True(result.IsSuccess);
        var score = game.GetPlayerScore(player);
        Assert.Equal(100, score.CurrentPoints);
        Assert.Equal(100, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);
        Assert.Equal(-75, result.Value.Points);
    }

    [Fact]
    public void RemovePoints_FallbackToCheckpoint_FallsBackToSecured()
    {
        var config = ScoringTestBase.Config(lossPolicy: LossPolicy.FallbackToCheckpoint);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 200, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 50, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.RemovePoints(player, PointTransactionType.AnswerIncorrect);

        Assert.True(result.IsSuccess);
        var score = game.GetPlayerScore(player);
        Assert.Equal(200, score.CurrentPoints);
        Assert.Equal(200, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);
        Assert.Equal(-50, result.Value.Points);
    }

    [Fact]
    public void RemovePoints_LoseAll_WithMixedPoints_DeductsEverything()
    {
        var config = ScoringTestBase.Config(lossPolicy: LossPolicy.LoseAll);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 200, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 50, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.RemovePoints(player, PointTransactionType.AnswerIncorrect);

        Assert.True(result.IsSuccess);
        var score = game.GetPlayerScore(player);
        Assert.Equal(0, score.CurrentPoints);
        Assert.Equal(0, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);
        Assert.Equal(-250, result.Value.Points);
        Assert.Equal(0, result.Value.ResultingBalance);
    }
}
