using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ScoringWithdrawalTests
{
    [Fact]
    public void Withdraw_KeepCurrentScore_NoDeduction()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 200, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Points);
        Assert.Equal(PointTransactionType.Withdrawal, result.Value.Type);
        Assert.Equal(500, game.GetPlayerScore(player).CurrentPoints);
        Assert.True(game.Players.Single(p => p.UserId == player).IsWithdrawn);
    }

    [Fact]
    public void Withdraw_LoseAll_DeductsEverything()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.LoseAll);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 200, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(-500, result.Value.Points);
        Assert.Equal(0, game.GetPlayerScore(player).CurrentPoints);
    }

    [Fact]
    public void Withdraw_KeepSecuredScore_DeductsUnsecuredOnly()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepSecuredScore);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 200, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(-200, result.Value.Points);
        var score = game.GetPlayerScore(player);
        Assert.Equal(300, score.CurrentPoints);
        Assert.Equal(300, score.SecuredPoints);
    }

    [Fact]
    public void Withdraw_KeepCheckpointScore_DeductsUnsecuredOnly()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepCheckpointScore);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 200, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(-200, result.Value.Points);
        Assert.Equal(300, game.GetPlayerScore(player).CurrentPoints);
    }

    [Fact]
    public void Withdraw_TerminalGame_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.Cancel("Test cancellation");

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidGameState.Code, result.Error.Code);
    }

    [Fact]
    public void Withdraw_AlreadyWithdrawn_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.WithdrawPlayer(player);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerAlreadyWithdrawn.Code, result.Error.Code);
    }

    [Fact]
    public void Withdraw_UnknownPlayer_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out _, out _);

        var result = game.WithdrawPlayer(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerNotInGame.Code, result.Error.Code);
    }

    [Fact]
    public void WithdrawnPlayer_CannotReceiveAwards()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.WithdrawPlayer(player);

        var result = game.AwardPoints(player, 100, PointTransactionType.RoundBonus);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerAlreadyWithdrawn.Code, result.Error.Code);
    }

    [Fact]
    public void Withdraw_SetsWithdrawnAt()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        game.WithdrawPlayer(player);

        var p = game.Players.Single(x => x.UserId == player);
        Assert.True(p.IsWithdrawn);
        Assert.NotNull(p.ExitedAt);
    }
}
