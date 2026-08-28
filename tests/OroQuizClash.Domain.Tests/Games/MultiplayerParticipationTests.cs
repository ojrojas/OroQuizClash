using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class MultiplayerParticipationTests
{
    [Fact]
    public void CurrentRoundNumber_BeforeFirstRound_IsZero()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);

        var p = game.Players.Single(x => x.UserId == player1);
        Assert.Equal(0, p.CurrentRoundNumber);
    }

    [Fact]
    public void StartRound_AdvancesCurrentRoundNumber_ForActivePlayers()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);

        var result = game.StartRound(question.Id.Value, 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, game.Players.Single(x => x.UserId == player1).CurrentRoundNumber);
        Assert.Equal(1, game.Players.Single(x => x.UserId == player2).CurrentRoundNumber);
    }

    [Fact]
    public void StartRound_SecondRound_AdvancesToTwo()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        var round1 = game.StartRound(q1.Id.Value, 1).Value;
        game.CompleteRound(round1.Id.Value);

        var result = game.StartRound(q2.Id.Value, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, game.Players.Single(x => x.UserId == player1).CurrentRoundNumber);
    }

    [Fact]
    public void StartRound_WithdrawnPlayer_CurrentRoundNumberFrozen()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2);
        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        var round1 = game.StartRound(q1.Id.Value, 1).Value;
        game.WithdrawPlayer(player1);
        game.CompleteRound(round1.Id.Value);

        game.StartRound(q2.Id.Value, 2);

        Assert.Equal(1, game.Players.Single(x => x.UserId == player1).CurrentRoundNumber);
        Assert.Equal(2, game.Players.Single(x => x.UserId == player2).CurrentRoundNumber);
    }

    [Fact]
    public void StartRound_EliminatedPlayer_CurrentRoundNumberFrozen()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2);
        var q1 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        var q2 = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        var round1 = game.StartRound(q1.Id.Value, 1).Value;
        game.EliminatePlayer(player1, "Loss policy");
        game.CompleteRound(round1.Id.Value);

        game.StartRound(q2.Id.Value, 2);

        Assert.Equal(1, game.Players.Single(x => x.UserId == player1).CurrentRoundNumber);
        Assert.Equal(2, game.Players.Single(x => x.UserId == player2).CurrentRoundNumber);
    }

    [Fact]
    public void GetPlayerAnswerState_NoActiveRound_NotAnswered()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);

        Assert.Equal(AnswerStatus.NotAnswered, game.GetPlayerAnswerState(player1));
    }

    [Fact]
    public void GetPlayerAnswerState_ActiveRoundNoSubmission_NotAnswered()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);

        Assert.Equal(AnswerStatus.NotAnswered, game.GetPlayerAnswerState(player1));
    }

    [Fact]
    public void GetPlayerAnswerState_AfterEvaluatedAnswer_Evaluated()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);

        var result = game.SubmitAnswer(
            player1,
            ScoringTestBase.CorrectOption(question),
            DateTimeOffset.UtcNow,
            ScoringTestBase.Resolver(question));

        Assert.True(result.IsSuccess);
        Assert.Equal(AnswerStatus.Evaluated, game.GetPlayerAnswerState(player1));
    }

    [Fact]
    public void GetPlayerAnswerState_AfterLateSubmission_Expired()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out _);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var lateTimestamp = DateTimeOffset.UtcNow.AddSeconds(game.Configuration.TimeLimitPerQuestionSeconds + 10);

        var result = game.SubmitAnswer(
            player1,
            ScoringTestBase.CorrectOption(question),
            lateTimestamp,
            ScoringTestBase.Resolver(question));

        Assert.True(result.IsFailure);
        Assert.Equal(AnswerStatus.Expired, game.GetPlayerAnswerState(player1));
    }

    [Fact]
    public void AnswerState_IsIndependent_PerPlayer()
    {
        var game = ScoringTestBase.CreateStartedGame(out var player1, out var player2);
        var question = ScoringTestBase.CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);

        game.SubmitAnswer(
            player1,
            ScoringTestBase.CorrectOption(question),
            DateTimeOffset.UtcNow,
            ScoringTestBase.Resolver(question));

        Assert.Equal(AnswerStatus.Evaluated, game.GetPlayerAnswerState(player1));
        Assert.Equal(AnswerStatus.NotAnswered, game.GetPlayerAnswerState(player2));
    }
}
