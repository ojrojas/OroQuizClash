using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class WithdrawalValidationTests
{
    [Fact]
    public void Withdraw_AlreadyWithdrawn_FailsPlayerAlreadyWithdrawn()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.WithdrawPlayer(player);

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerAlreadyWithdrawn.Code, result.Error.Code);
    }

    [Fact]
    public void Withdraw_EliminatedPlayer_FailsPlayerAlreadyEliminated()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.EliminatePlayer(player, "Tournament elimination");

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerAlreadyEliminated.Code, result.Error.Code);
    }

    [Fact]
    public void Withdraw_TerminalGame_FailsInvalidGameState()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.Cancel("Test cancellation");

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidGameState.Code, result.Error.Code);
    }

    [Fact]
    public void Withdraw_FinishedGame_FailsInvalidGameState()
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

        var result = game.WithdrawPlayer(player);

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidGameState.Code, result.Error.Code);
    }

    [Fact]
    public void Withdraw_WinnerPlayer_FailsParticipationAlreadyFinished()
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

        var p = game.Players.Single(x => x.UserId == player);
        Assert.Equal(PlayerParticipationStatus.Winner, p.ParticipationStatus);

        var result = game.WithdrawPlayer(player);
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Withdraw_UnknownPlayer_FailsPlayerNotInGame()
    {
        var game = ScoringTestBase.CreateStartedGame(out _, out _);

        var result = game.WithdrawPlayer(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerNotInGame.Code, result.Error.Code);
    }

    [Fact]
    public void Withdraw_RejectedCases_NoStateChange()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.AwardPoints(player, 100, PointTransactionType.RoundBonus, roundScoped: false);
        game.EliminatePlayer(player, "Eliminated");
        var transactionsBefore = game.PointTransactions.Count;

        game.WithdrawPlayer(player);

        Assert.Equal(transactionsBefore, game.PointTransactions.Count);
        Assert.Equal(PlayerParticipationStatus.Eliminated, game.Players.Single(x => x.UserId == player).ParticipationStatus);
        Assert.Equal(100, game.GetPlayerScore(player).CurrentPoints);
    }
}
