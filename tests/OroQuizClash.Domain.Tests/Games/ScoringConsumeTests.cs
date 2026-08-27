using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringConsumeTests
{
    [Fact]
    public void ConsumePoints_SufficientBalance_DeductsAtomically()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 500, PointTransactionType.RoundBonus, roundScoped: false);

        var result = game.ConsumePoints(player, 300, "Reward redemption");

        Assert.True(result.IsSuccess);
        Assert.Equal(-300, result.Value.Points);
        Assert.Equal(PointTransactionType.RewardRedemption, result.Value.Type);
        Assert.Equal(200, result.Value.ResultingBalance);

        var score = game.GetPlayerScore(player);
        Assert.Equal(200, score.CurrentPoints);
    }

    [Fact]
    public void ConsumePoints_InsufficientBalance_FailsWithoutPartialDeduction()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 200, PointTransactionType.RoundBonus, roundScoped: false);

        var result = game.ConsumePoints(player, 300, "Reward redemption");

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InsufficientPoints.Code, result.Error.Code);

        var score = game.GetPlayerScore(player);
        Assert.Equal(200, score.CurrentPoints);
        Assert.DoesNotContain(game.PointTransactions, pt => pt.PlayerId == player && pt.Type == PointTransactionType.RewardRedemption);
    }

    [Fact]
    public void ConsumePoints_ExactBalance_Succeeds()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);

        var result = game.ConsumePoints(player, 300, "Reward redemption");

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.ResultingBalance);
        Assert.Equal(0, game.GetPlayerScore(player).CurrentPoints);
    }

    [Fact]
    public void ConsumePoints_ZeroAmount_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        var result = game.ConsumePoints(player, 0, "Reward redemption");

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidAdjustmentAmount.Code, result.Error.Code);
    }

    [Fact]
    public void ConsumePoints_UnknownPlayer_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out _, out _);

        var result = game.ConsumePoints(Guid.NewGuid(), 100, "Reward redemption");

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerNotInGame.Code, result.Error.Code);
    }

    [Fact]
    public void ConsumePoints_DeductsFromSecuredFirst()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 100, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 50, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.ConsumePoints(player, 120, "Reward redemption");

        Assert.True(result.IsSuccess);
        var score = game.GetPlayerScore(player);
        Assert.Equal(30, score.CurrentPoints);
        Assert.Equal(0, score.SecuredPoints);
        Assert.Equal(30, score.RoundPoints);
    }
}
