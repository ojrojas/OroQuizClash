using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringSecureTests
{
    [Fact]
    public void CompleteRound_SecuresRoundPoints()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));

        game.CompleteRound(game.CurrentRound!.Id.Value);

        var score = game.GetPlayerScore(player);
        Assert.Equal(100, score.SecuredPoints);
        Assert.Equal(0, score.RoundPoints);
        Assert.Equal(100, score.CurrentPoints);
        Assert.Contains(game.DomainEvents, e => e is PointsSecuredDomainEvent pse && pse.PlayerId == player && pse.SecuredAmount == 100);
    }

    [Fact]
    public void SecurePoints_NoRoundPoints_IsNoOp()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        var result = game.SecurePoints(player);

        Assert.True(result.IsSuccess);
        var score = game.GetPlayerScore(player);
        Assert.Equal(0, score.SecuredPoints);
    }

    [Fact]
    public void SecurePoints_UnknownPlayer_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out _, out _);

        var result = game.SecurePoints(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerNotInGame.Code, result.Error.Code);
    }

    [Fact]
    public void SecuredPoints_SurviveLossUnderLoseUnsecuredPolicy()
    {
        var config = ScoringTestBase.Config(lossPolicy: LossPolicy.LoseUnsecuredPoints);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q1, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q1), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q1));
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q2, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.IncorrectOption(q2), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q2));

        var score = game.GetPlayerScore(player);
        Assert.Equal(100, score.SecuredPoints);
        Assert.Equal(100, score.CurrentPoints);
    }

    [Fact]
    public void CompleteRound_ProgressiveBonus_AwardsRoundBonus()
    {
        var config = ScoringTestBase.Config(scoringSystem: ScoringSystem.ProgressiveBonus, pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));

        game.CompleteRound(game.CurrentRound!.Id.Value);

        var bonus = game.PointTransactions.Single(pt => pt.PlayerId == player && pt.Type == PointTransactionType.RoundBonus);
        Assert.Equal(1, bonus.Points);

        var score = game.GetPlayerScore(player);
        Assert.Equal(101, score.CurrentPoints);
    }

    [Fact]
    public void CompleteRound_DifficultyIncrease_AwardsLevelBonus()
    {
        var config = ScoringTestBase.Config(pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);

        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q1, difficulty: 1);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q1), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q1));
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q2, difficulty: 2);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q2), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q2));
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var levelBonus = game.PointTransactions.Single(pt => pt.PlayerId == player && pt.Type == PointTransactionType.LevelBonus);
        Assert.Equal(100, levelBonus.Points);
    }

    [Fact]
    public void CompleteRound_SameDifficulty_NoLevelBonus()
    {
        var config = ScoringTestBase.Config(pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);

        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q1, difficulty: 2);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q1), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q1));
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, q2, difficulty: 2);
        game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q2), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q2));
        game.CompleteRound(game.CurrentRound!.Id.Value);

        Assert.DoesNotContain(game.PointTransactions, pt => pt.PlayerId == player && pt.Type == PointTransactionType.LevelBonus);
    }

    [Fact]
    public void CompleteRound_WithdrawnPlayer_NotSecured()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        ScoringTestBase.StartRoundWithQuestion(game, question, difficulty: 1);
        game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(question), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(question));
        game.WithdrawPlayer(player2);

        game.CompleteRound(game.CurrentRound!.Id.Value);

        Assert.Contains(game.PointTransactions, pt => pt.PlayerId == player1 && pt.Type == PointTransactionType.AnswerCorrect);
        Assert.DoesNotContain(game.DomainEvents, e => e is PointsSecuredDomainEvent pse && pse.PlayerId == player2);
    }
}
