using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Shared.Errors;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ParticipationLifecycleTests
{
    [Fact]
    public void JoinPlayer_SetsStatusActive()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        var p = game.Players.Single(x => x.UserId == player);
        Assert.Equal(PlayerParticipationStatus.Active, p.ParticipationStatus);
        Assert.True(p.IsActive);
        Assert.Null(p.ExitedAt);
    }

    [Fact]
    public void Withdraw_TransitionsToWithdrawn_Terminal()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        game.WithdrawPlayer(player);

        var p = game.Players.Single(x => x.UserId == player);
        Assert.Equal(PlayerParticipationStatus.Withdrawn, p.ParticipationStatus);
        Assert.True(p.ParticipationStatus.IsTerminalParticipation);
        Assert.NotNull(p.ExitedAt);
    }

    [Fact]
    public void EliminatePlayer_TransitionsToEliminated_Terminal()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);

        var result = game.EliminatePlayer(player, "Tournament rules");

        Assert.True(result.IsSuccess);
        var p = game.Players.Single(x => x.UserId == player);
        Assert.Equal(PlayerParticipationStatus.Eliminated, p.ParticipationStatus);
        Assert.True(p.ParticipationStatus.IsTerminalParticipation);
        Assert.NotNull(p.ExitedAt);
        Assert.Contains(game.DomainEvents, e => e is PlayerEliminatedDomainEvent ped && ped.PlayerId == player && ped.Reason == "Tournament rules");
    }

    [Fact]
    public void EliminatePlayer_TerminalGame_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.Cancel("Cancelled");

        var result = game.EliminatePlayer(player, "Too late");

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.InvalidGameState.Code, result.Error.Code);
    }

    [Fact]
    public void EliminatePlayer_UnknownPlayer_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out _, out _);

        var result = game.EliminatePlayer(Guid.NewGuid(), "Not here");

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerNotInGame.Code, result.Error.Code);
    }

    [Fact]
    public void EliminatePlayer_AlreadyWithdrawn_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.WithdrawPlayer(player);

        var result = game.EliminatePlayer(player, "Too late");

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerAlreadyWithdrawn.Code, result.Error.Code);
    }

    [Fact]
    public void EliminatePlayer_AlreadyEliminated_Fails()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player, out _);
        game.EliminatePlayer(player, "First elimination");

        var result = game.EliminatePlayer(player, "Second elimination");

        Assert.True(result.IsFailure);
        Assert.Equal(GameErrors.PlayerAlreadyEliminated.Code, result.Error.Code);
    }

    [Fact]
    public void Finish_TopScorer_BecomesWinner()
    {
        var config = ScoringTestBase.Config(minRounds: 5, pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);

        for (var i = 0; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q);
            game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }

        game.Finish();

        var winner = game.Players.Single(x => x.UserId == player1);
        var loser = game.Players.Single(x => x.UserId == player2);
        Assert.Equal(PlayerParticipationStatus.Winner, winner.ParticipationStatus);
        Assert.NotEqual(PlayerParticipationStatus.Winner, loser.ParticipationStatus);
    }

    [Fact]
    public void Finish_TiedScores_AllBecomeWinners()
    {
        var config = ScoringTestBase.Config(minRounds: 5, pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);

        for (var i = 0; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q);
            game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.SubmitAnswer(player2, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }

        game.Finish();

        Assert.All(game.Players, p => Assert.Equal(PlayerParticipationStatus.Winner, p.ParticipationStatus));
    }

    [Fact]
    public void Finish_WithdrawnPlayer_NeverWinner()
    {
        var config = ScoringTestBase.Config(minRounds: 5, pointsPerRound: 100, withdrawalPolicy: WithdrawalPolicy.KeepCurrentScore);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);

        for (var i = 0; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q);
            game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.SubmitAnswer(player2, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }
        game.WithdrawPlayer(player2);

        game.Finish();

        var withdrawn = game.Players.Single(x => x.UserId == player2);
        Assert.Equal(PlayerParticipationStatus.Withdrawn, withdrawn.ParticipationStatus);
    }

    [Fact]
    public void Finish_EliminatedPlayer_NeverWinner()
    {
        var config = ScoringTestBase.Config(minRounds: 5, pointsPerRound: 100);
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2, config);

        for (var i = 0; i < 5; i++)
        {
            var q = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
            ScoringTestBase.StartRoundWithQuestion(game, q);
            game.SubmitAnswer(player1, ScoringTestBase.CorrectOption(q), DateTimeOffset.UtcNow, ScoringTestBase.Resolver(q));
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }
        game.AwardPoints(player2, 9999, PointTransactionType.Adjustment, reason: "Test setup");
        game.EliminatePlayer(player2, "Eliminated before finish");

        game.Finish();

        var eliminated = game.Players.Single(x => x.UserId == player2);
        Assert.Equal(PlayerParticipationStatus.Eliminated, eliminated.ParticipationStatus);
    }
}
