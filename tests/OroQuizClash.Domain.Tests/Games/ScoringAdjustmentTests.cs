using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringAdjustmentTests
{
    [Fact]
    public void AdjustPoints_Positive_AwardsWithReason()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        var admin = Guid.NewGuid();

        var result = game.AdjustPoints(player, 100, "System error correction", admin);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value.Points);
        Assert.Equal(PointTransactionType.Adjustment, result.Value.Type);
        Assert.Equal("System error correction", result.Value.Reason);
        Assert.Equal(100, game.GetPlayerScore(player).CurrentPoints);
    }

    [Fact]
    public void AdjustPoints_Negative_DeductsWithReason()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 200, PointTransactionType.RoundBonus, roundScoped: false);

        var result = game.AdjustPoints(player, -50, "Duplicate points correction", Guid.NewGuid());

        Assert.True(result.IsSuccess);
        Assert.Equal(-50, result.Value.Points);
        Assert.Equal(150, game.GetPlayerScore(player).CurrentPoints);
    }

    [Fact]
    public void AdjustPoints_EmptyReason_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        var result = game.AdjustPoints(player, 100, "", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.AdjustmentReasonRequired.Code, result.Error.Code);
    }

    [Fact]
    public void AdjustPoints_ShortReason_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        var result = game.AdjustPoints(player, 100, "ab", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.AdjustmentReasonRequired.Code, result.Error.Code);
    }

    [Fact]
    public void AdjustPoints_ZeroAmount_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        var result = game.AdjustPoints(player, 0, "Valid reason here", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidAdjustmentAmount.Code, result.Error.Code);
    }

    [Fact]
    public void AdjustPoints_NegativeExceedingBalance_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 50, PointTransactionType.RoundBonus, roundScoped: false);

        var result = game.AdjustPoints(player, -100, "Valid reason here", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InsufficientPoints.Code, result.Error.Code);
    }

    [Fact]
    public void AdjustPoints_FinishedGame_StillAllowed()
    {
        var config = ScoringTestBase.Config(minRounds: 5);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        for (var i = 0; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q);
            game.SubmitAnswer(player, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }
        game.Finish();

        var result = game.AdjustPoints(player, 100, "Post-game correction", Guid.NewGuid());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void AdjustPoints_UnknownPlayer_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out _, out _);

        var result = game.AdjustPoints(Guid.NewGuid(), 100, "Valid reason here", Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerNotInGame.Code, result.Error.Code);
    }
}
