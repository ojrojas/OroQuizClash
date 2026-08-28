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

public sealed class GetPlayerStateHandlerTests
{
    private static Game CreateGameWithPlayer(out Guid playerId)
    {
        var config = new GameConfiguration(
            "Multiplayer Game",
            new CategoryId(Guid.NewGuid()),
            5, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            2, 10, 100);

        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        playerId = Guid.NewGuid();
        game.JoinPlayer(playerId, "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
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

    private static IRepository<Game, GameId> RepoReturning(Game? game)
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(game));
        return repo;
    }

    [Fact]
    public async Task GetPlayerState_MidGame_ReturnsFullState()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        game.SubmitAnswer(playerId, option, DateTimeOffset.UtcNow, qid => qid == question.Id ? question : null);
        var handler = new GetPlayerStateHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetPlayerStateQuery(game.Id.Value, playerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var state = result.Value;
        Assert.Equal("ACTIVE", state.Status);
        Assert.Equal(1, state.CurrentRound);
        Assert.Equal("EVALUATED", state.AnswerState);
        Assert.Equal(1, state.CorrectAnswers);
        Assert.Equal(0, state.IncorrectAnswers);
        Assert.True(state.CurrentPoints > 0);
        Assert.Equal(state.CurrentPoints, state.TotalPoints);
        Assert.Null(state.ExitedAt);
    }

    [Fact]
    public async Task GetPlayerState_WithdrawnPlayer_FrozenRoundAndExitedAt()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var q1 = CreateQuestion(game.Configuration.CategoryId);
        var q2 = CreateQuestion(game.Configuration.CategoryId);
        var round1 = game.StartRound(q1.Id.Value, 1).Value;
        game.WithdrawPlayer(playerId);
        game.CompleteRound(round1.Id.Value);
        game.StartRound(q2.Id.Value, 2);
        var handler = new GetPlayerStateHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetPlayerStateQuery(game.Id.Value, playerId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("WITHDRAWN", result.Value.Status);
        Assert.Equal(1, result.Value.CurrentRound);
        Assert.NotNull(result.Value.ExitedAt);
    }

    [Fact]
    public async Task GetPlayerState_GameNotFound_Fails()
    {
        var handler = new GetPlayerStateHandler(RepoReturning(null));

        var result = await handler.HandleAsync(new GetPlayerStateQuery(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("GameNotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetPlayerState_PlayerNotInGame_Fails()
    {
        var game = CreateGameWithPlayer(out _);
        var handler = new GetPlayerStateHandler(RepoReturning(game));

        var result = await handler.HandleAsync(new GetPlayerStateQuery(game.Id.Value, Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlayerNotInGame", result.Error.Code);
    }
}
