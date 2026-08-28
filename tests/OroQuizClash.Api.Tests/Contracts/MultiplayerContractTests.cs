using System.Text.Json;

using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;
using OroQuizClash.Infrastructure.Persistence;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Api.Tests.Contracts;

/// <summary>
/// Verifies the multiplayer read contract shapes against
/// specs/011-multiplayer/contracts/multiplayer.openapi.yaml.
/// </summary>
public sealed class MultiplayerContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static OroQuizClashDbContext CreateContext()
    {
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new OroQuizClashDbContext(options, dispatcher);
    }

    private static Question CreateQuestion(CategoryId categoryId) =>
        Question.Create(
            "Contract question?",
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

    private static async Task<(OroQuizClashDbContext ctx, Game game, Guid playerA, Guid playerB)> SeedGameWithEvaluatedRoundAsync()
    {
        var ctx = CreateContext();
        var config = new GameConfiguration(
            "Contract Game",
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
        var playerA = Guid.NewGuid();
        var playerB = Guid.NewGuid();
        game.JoinPlayer(playerA, "Alice");
        game.JoinPlayer(playerB, "Bob");
        game.Start();

        var question = CreateQuestion(config.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        var answer = game.SubmitAnswer(playerA, option, DateTimeOffset.UtcNow, qid => question);
        Assert.True(answer.IsSuccess);
        game.CompleteRound(game.CurrentRound!.Id.Value);

        var repo = new EfRepository<Game, GameId>(ctx);
        await repo.AddAsync(game, CancellationToken.None);
        await ctx.SaveChangesAsync();
        return (ctx, game, playerA, playerB);
    }

    [Fact]
    public async Task Leaderboard_ResponseShape_MatchesOpenApiContract()
    {
        var (ctx, game, playerA, playerB) = await SeedGameWithEvaluatedRoundAsync();

        var handler = new GetLeaderboardHandler(new EfRepository<Game, GameId>(ctx));

        var result = await handler.HandleAsync(new GetLeaderboardQuery(game.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value, JsonOptions)).RootElement;

        Assert.Equal(game.Id.Value, json.GetProperty("gameId").GetGuid());
        var players = json.GetProperty("players");
        Assert.Equal(2, players.GetArrayLength());

        var entry = players[0];
        var expectedProperties = new[]
        {
            "playerId", "displayName", "rank", "points",
            "correctAnswers", "currentLevel", "status", "securedPoints"
        };
        foreach (var property in expectedProperties)
            Assert.True(entry.TryGetProperty(property, out _), $"missing property '{property}'");

        Assert.Equal(playerA, entry.GetProperty("playerId").GetGuid());
        Assert.Equal(1, entry.GetProperty("rank").GetInt32());
        Assert.Equal(100, entry.GetProperty("points").GetInt32());
        Assert.Equal(1, entry.GetProperty("correctAnswers").GetInt32());
        Assert.Equal(1, entry.GetProperty("currentLevel").GetInt32());
        Assert.Equal("ACTIVE", entry.GetProperty("status").GetString());
        Assert.Equal(100, entry.GetProperty("securedPoints").GetInt32());

        var second = players[1];
        Assert.Equal(playerB, second.GetProperty("playerId").GetGuid());
        Assert.Equal(2, second.GetProperty("rank").GetInt32());
        Assert.Equal(0, second.GetProperty("points").GetInt32());
        Assert.Equal(0, second.GetProperty("correctAnswers").GetInt32());
    }

    [Fact]
    public async Task PlayerState_ResponseShape_MatchesOpenApiContract()
    {
        var (ctx, game, playerA, _) = await SeedGameWithEvaluatedRoundAsync();

        var handler = new GetPlayerStateHandler(new EfRepository<Game, GameId>(ctx));

        var result = await handler.HandleAsync(new GetPlayerStateQuery(game.Id.Value, playerA), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value, JsonOptions)).RootElement;

        var expectedProperties = new[]
        {
            "gameId", "playerId", "displayName", "status",
            "currentPoints", "securedPoints", "roundPoints", "potentialPoints",
            "totalPoints", "currentRound", "answerState",
            "correctAnswers", "incorrectAnswers", "exitedAt"
        };
        foreach (var property in expectedProperties)
            Assert.True(json.TryGetProperty(property, out _), $"missing property '{property}'");

        Assert.Equal(game.Id.Value, json.GetProperty("gameId").GetGuid());
        Assert.Equal(playerA, json.GetProperty("playerId").GetGuid());
        Assert.Equal("Alice", json.GetProperty("displayName").GetString());
        Assert.Equal("ACTIVE", json.GetProperty("status").GetString());
        Assert.Equal(100, json.GetProperty("currentPoints").GetInt32());
        Assert.Equal(100, json.GetProperty("securedPoints").GetInt32());
        Assert.Equal(1, json.GetProperty("currentRound").GetInt32());
        // Round 1 is completed, no round in progress -> current-round state resets
        Assert.Equal("NOT_ANSWERED", json.GetProperty("answerState").GetString());
        Assert.Equal(1, json.GetProperty("correctAnswers").GetInt32());
        Assert.Equal(0, json.GetProperty("incorrectAnswers").GetInt32());
        Assert.Equal(JsonValueKind.Null, json.GetProperty("exitedAt").ValueKind);
    }

    [Fact]
    public async Task PlayerState_WithdrawnPlayer_ShowsWithdrawnStatusAndExitedAt()
    {
        var (ctx, game, _, playerB) = await SeedGameWithEvaluatedRoundAsync();


        var loaded = await new EfRepository<Game, GameId>(ctx)
            .FirstOrDefaultAsync(new GameByIdWithAnswersSpecification(game.Id), CancellationToken.None);
        Assert.NotNull(loaded);
        loaded!.WithdrawPlayer(playerB);
        await ctx.SaveChangesAsync();

        var handler = new GetPlayerStateHandler(new EfRepository<Game, GameId>(ctx));
        var result = await handler.HandleAsync(new GetPlayerStateQuery(game.Id.Value, playerB), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var json = JsonDocument.Parse(JsonSerializer.Serialize(result.Value, JsonOptions)).RootElement;
        Assert.Equal("WITHDRAWN", json.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.String, json.GetProperty("exitedAt").ValueKind);
    }
}
