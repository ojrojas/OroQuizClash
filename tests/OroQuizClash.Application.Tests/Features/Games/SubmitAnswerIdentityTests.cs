using System.Security.Claims;

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

public sealed class SubmitAnswerIdentityTests
{
    private static Game CreateGameWithPlayer(out Guid playerId)
    {
        var config = new GameConfiguration(
            "Identity Game",
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

    private static (SubmitAnswerHandler handler, Game game, Question question) CreateHandler(Game game, Question question)
    {
        var gameRepo = Substitute.For<IRepository<Game, GameId>>();
        gameRepo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Game?>(game));

        var questionRepo = Substitute.For<IRepository<Question, Domain.Questions.QuestionId>>();
        questionRepo.FirstOrDefaultAsync(Arg.Any<ISpecification<Question>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Question?>(question));

        var uow = Substitute.For<IUnitOfWork>();
        return (new SubmitAnswerHandler(gameRepo, questionRepo, uow), game, question);
    }

    [Fact]
    public async Task SubmitAnswer_UsesAuthenticatedPlayerId_AnswerBelongsToThatPlayer()
    {
        var game = CreateGameWithPlayer(out var playerId);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var (handler, _, _) = CreateHandler(game, question);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;

        var result = await handler.HandleAsync(
            new SubmitAnswerCommand(game.Id.Value, playerId, option.Value, null, null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var answer = game.Answers.Single();
        Assert.Equal(playerId, answer.PlayerId);
        Assert.True(answer.Correct);
    }

    [Fact]
    public async Task SubmitAnswer_PlayerNotInGame_Fails()
    {
        var game = CreateGameWithPlayer(out _);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var (handler, _, _) = CreateHandler(game, question);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;

        var result = await handler.HandleAsync(
            new SubmitAnswerCommand(game.Id.Value, Guid.NewGuid(), option.Value, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("PlayerNotInGame", result.Error.Code);
        Assert.Empty(game.Answers);
    }

    [Fact]
    public async Task SubmitAnswer_PlayerIdEmpty_Fails()
    {
        var game = CreateGameWithPlayer(out _);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var (handler, _, _) = CreateHandler(game, question);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;

        var result = await handler.HandleAsync(
            new SubmitAnswerCommand(game.Id.Value, Guid.Empty, option.Value, null, null),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Empty(game.Answers);
    }

    [Fact]
    public void GetSub_ParsesSubClaim()
    {
        var id = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", id.ToString())]));

        Assert.Equal(id, GameClaims.GetSub(user));
    }

    [Fact]
    public void GetSub_FallsBackToNameIdentifier()
    {
        var id = Guid.NewGuid();
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())]));

        Assert.Equal(id, GameClaims.GetSub(user));
    }

    [Fact]
    public void GetSub_NoClaims_ReturnsEmpty()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Equal(Guid.Empty, GameClaims.GetSub(user));
    }

    [Theory]
    [InlineData("role", "ADMIN", true)]
    [InlineData("role", "GAME_MANAGER", true)]
    [InlineData("roles", "ADMIN", true)]
    [InlineData("roles", "GAME_MANAGER", true)]
    [InlineData("role", "PLAYER", false)]
    [InlineData("roles", "REWARD_MANAGER", false)]
    public void IsOrganizer_DependsOnRoleClaim(string claimType, string role, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, role)]));

        Assert.Equal(expected, GameClaims.IsOrganizer(user));
    }

    [Fact]
    public void IsOrganizer_NoClaims_False()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.False(GameClaims.IsOrganizer(user));
    }
}
