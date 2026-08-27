using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class WithdrawalRetentionTests
{
    [Fact]
    public void Withdraw_KeepSecuredScore_RetainsEligiblePoints()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepSecuredScore);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 200, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(300, game.GetPlayerScore(player).CurrentPoints);
        Assert.Equal(300, result.Value.ResultingBalance);
    }

    [Fact]
    public void Withdraw_KeepCurrentScore_RetainsAllPoints()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 500, PointTransactionType.RoundBonus, roundScoped: false);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(500, game.GetPlayerScore(player).CurrentPoints);
    }

    [Fact]
    public void Withdraw_LoseAll_RetainsZero()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.LoseAll);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 500, PointTransactionType.RoundBonus, roundScoped: false);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, game.GetPlayerScore(player).CurrentPoints);
    }

    [Fact]
    public void Withdraw_KeepCheckpointScore_RetainsSecured()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepCheckpointScore);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 200, PointTransactionType.AnswerCorrect, roundScoped: true);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(300, game.GetPlayerScore(player).CurrentPoints);
    }

    [Fact]
    public void Withdraw_CreatesWithdrawalTransaction_WithPolicyInReason()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepSecuredScore);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 200, PointTransactionType.AnswerCorrect, roundScoped: true);

        game.WithdrawPlayer(player);

        var transaction = game.PointTransactions.Single(pt => pt.PlayerId == player && pt.Type == PointTransactionType.Withdrawal);
        Assert.Equal(-200, transaction.Points);
        Assert.Contains("KEEP_SECURED_SCORE", transaction.Reason);
    }

    [Fact]
    public void Withdraw_SetsStatusWithdrawn_AndExitedAt()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        game.WithdrawPlayer(player);

        var p = game.Players.Single(x => x.UserId == player);
        Assert.Equal(PlayerParticipationStatus.Withdrawn, p.ParticipationStatus);
        Assert.True(p.IsWithdrawn);
        Assert.False(p.IsActive);
        Assert.NotNull(p.ExitedAt);
    }

    [Fact]
    public void Withdraw_RaisesPlayerWithdrawnDomainEvent()
    {
        var config = ScoringTestBase.Config(withdrawalPolicy: WithdrawalPolicy.KeepSecuredScore);
        var game = ScoringTestBase.CreateStartedGame(out var player, out _, config);
        game.AwardPoints(player, 300, PointTransactionType.RoundBonus, roundScoped: false);
        game.AwardPoints(player, 200, PointTransactionType.AnswerCorrect, roundScoped: true);

        game.WithdrawPlayer(player);

        var evt = game.DomainEvents.OfType<PlayerWithdrawnDomainEvent>().Single();
        Assert.Equal(player, evt.PlayerId);
        Assert.Equal(300, evt.RetainedPoints);
        Assert.Equal("KEEP_SECURED_SCORE", evt.PolicyName);
    }

    [Fact]
    public void Withdraw_ZeroPoints_SucceedsWithZeroDeduction()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Points);
        Assert.Equal(0, game.GetPlayerScore(player).CurrentPoints);
        Assert.Contains(game.PointTransactions, pt => pt.PlayerId == player && pt.Type == PointTransactionType.Withdrawal);
    }

    [Fact]
    public void Withdraw_IsIrreversible_StatusIsTerminal()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.WithdrawPlayer(player);

        var p = game.Players.Single(x => x.UserId == player);
        Assert.True(p.ParticipationStatus.IsTerminalParticipation);
    }
}
