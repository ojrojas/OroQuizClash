using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Specifications;

using NSubstitute;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class LeaderboardRankingTests
{
    private static Game CreateGame(out Guid playerA, out Guid playerB, out Guid playerC)
    {
        var config = new GameConfiguration(
            "Leaderboard Game",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseCurrentRound,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10, 100);

        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        playerA = Guid.NewGuid();
        playerB = Guid.NewGuid();
        playerC = Guid.NewGuid();
        game.JoinPlayer(playerA, "Alice");
        game.JoinPlayer(playerB, "Bob");
        game.JoinPlayer(playerC, "Carol");
        game.Start();
        return game;
    }

    private static Question CreateQuestion(CategoryId categoryId) =>
        Question.Create(
            "Test question?",
            categoryId,
            Domain.Questions.ValueObjects.DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            new (string text, bool isCorrect, int displayOrder)[]
            {
                ("Correct answer", true, 0),
                ("Wrong B", false, 1),
                ("Wrong C", false, 2),
                ("Wrong D", false, 3)
            },
            Guid.NewGuid()).Value;

    private static GetLeaderboardHandler HandlerFor(Game game)
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Game?>(game));
        return new GetLeaderboardHandler(repo);
    }

    private static void AnswerRound(Game game, Guid playerId, Question question, bool correct)
    {
        var option = question.AnswerOptions.First(o => o.IsCorrect == correct).Id;
        var result = game.SubmitAnswer(playerId, option, DateTimeOffset.UtcNow, qid => qid == question.Id ? question : null);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Leaderboard_OrdersByPointsDescending()
    {
        var game = CreateGame(out var playerA, out var playerB, out var playerC);
        var q1 = CreateQuestion(game.Configuration.CategoryId);
        var q2 = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(q1.Id.Value, 1);
        AnswerRound(game, playerA, q1, correct: true);
        AnswerRound(game, playerB, q1, correct: true);
        AnswerRound(game, playerC, q1, correct: false);
        game.CompleteRound(game.CurrentRound!.Id.Value);

        game.StartRound(q2.Id.Value, 2);
        AnswerRound(game, playerA, q2, correct: true);
        AnswerRound(game, playerB, q2, correct: false);
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var result = await HandlerFor(game).HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var board = result.Value.Players;
        Assert.Equal(3, board.Count);
        Assert.Equal(playerA, board[0].PlayerId);
        Assert.Equal(1, board[0].Rank);
        Assert.Equal(playerB, board[1].PlayerId);
        Assert.Equal(2, board[1].Rank);
        Assert.Equal(playerC, board[2].PlayerId);
        Assert.Equal(3, board[2].Rank);
        Assert.True(board[0].Points > board[1].Points);
        Assert.True(board[1].Points > board[2].Points);
    }

    [Fact]
    public async Task Leaderboard_TieOnPoints_BreaksByCorrectAnswers()
    {
        var game = CreateGame(out var playerA, out var playerB, out var playerC);
        var q1 = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(q1.Id.Value, 1);
        AnswerRound(game, playerA, q1, correct: true); // 100 points, 1 correct
        game.CompleteRound(game.CurrentRound!.Id.Value);

        // Player C reaches the same points via administrative adjustment, without correct answers
        var adjust = game.AdjustPoints(playerC, 100, "Test adjustment", Guid.NewGuid());
        Assert.True(adjust.IsSuccess);

        var result = await HandlerFor(game).HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var entryA = result.Value.Players.Single(e => e.PlayerId == playerA);
        var entryC = result.Value.Players.Single(e => e.PlayerId == playerC);
        Assert.Equal(entryA.Points, entryC.Points);
        Assert.Equal(1, entryA.CorrectAnswers);
        Assert.Equal(0, entryC.CorrectAnswers);
        Assert.True(entryA.Rank < entryC.Rank);
        _ = playerB;
    }

    [Fact]
    public async Task Leaderboard_WithdrawnAndEliminatedPlayers_RemainWithStatus()
    {
        var game = CreateGame(out var playerA, out var playerB, out var playerC);
        var q1 = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(q1.Id.Value, 1);
        AnswerRound(game, playerA, q1, correct: true);
        AnswerRound(game, playerB, q1, correct: true);
        game.WithdrawPlayer(playerB);
        game.EliminatePlayer(playerC, "Loss policy");
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var result = await HandlerFor(game).HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var board = result.Value.Players;
        Assert.Equal(3, board.Count);
        Assert.Equal("WITHDRAWN", board.Single(e => e.PlayerId == playerB).Status);
        Assert.Equal("ELIMINATED", board.Single(e => e.PlayerId == playerC).Status);
        Assert.Equal("ACTIVE", board.Single(e => e.PlayerId == playerA).Status);
    }

    [Fact]
    public async Task Leaderboard_CurrentLevel_ReflectsPlayerRoundDifficulty()
    {
        var game = CreateGame(out var playerA, out var playerB, out _);
        var q1 = CreateQuestion(game.Configuration.CategoryId);
        var q2 = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(q1.Id.Value, 1);
        game.WithdrawPlayer(playerB);
        game.CompleteRound(game.CurrentRound!.Id.Value);
        game.StartRound(q2.Id.Value, 2);

        var result = await HandlerFor(game).HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Players.Single(e => e.PlayerId == playerA).CurrentLevel);
        Assert.Equal(1, result.Value.Players.Single(e => e.PlayerId == playerB).CurrentLevel);
    }

    [Fact]
    public async Task Leaderboard_BeforeFirstRound_CurrentLevelNull()
    {
        var game = CreateGame(out var playerA, out _, out _);

        var result = await HandlerFor(game).HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Players.Single(e => e.PlayerId == playerA).CurrentLevel);
        Assert.Equal(0, result.Value.Players.Single(e => e.PlayerId == playerA).Points);
    }

    [Fact]
    public async Task Leaderboard_IsDeterministic_AcrossRepeatedQueries()
    {
        var game = CreateGame(out var playerA, out var playerB, out var playerC);
        var q1 = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(q1.Id.Value, 1);
        AnswerRound(game, playerA, q1, correct: true);
        AnswerRound(game, playerB, q1, correct: true);
        AnswerRound(game, playerC, q1, correct: false);
        game.CompleteRound(game.CurrentRound!.Id.Value);
        var handler = HandlerFor(game);

        var first = await handler.HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);
        var second = await handler.HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);

        Assert.True(first.IsSuccess && second.IsSuccess);
        Assert.Equal(
            first.Value.Players.Select(e => (e.PlayerId, e.Rank)),
            second.Value.Players.Select(e => (e.PlayerId, e.Rank)));
    }

    [Fact]
    public async Task Leaderboard_GameNotFound_Fails()
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Game?>(null));
        var handler = new GetLeaderboardHandler(repo);

        var result = await handler.HandleAsync(new GetLeaderboardQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("GameNotFound", result.Error.Code);
    }
}
