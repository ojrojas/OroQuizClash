using BuildingBlocks.Kernel.Domain.Repositories;

using NSubstitute;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class ConsolidationHistoryHandlerTests
{
    private readonly IRepository<Game, GameId> _gameRepo = Substitute.For<IRepository<Game, GameId>>();

    [Fact]
    public async Task GetHistory_PlayerWithConsolation_ReturnsHistory()
    {
        var game = CreateGameWithConsolation(out var loserId);
        _gameRepo.ListAsync(Arg.Any<AllGamesWithPlayerSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new List<Game> { game });

        var handler = new GetPlayerConsolationHistoryHandler(_gameRepo);
        var result = await handler.HandleAsync(
            new GetPlayerConsolationHistoryQuery(loserId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Consolations);
        Assert.Equal("FixedPoints", result.Value.Consolations.First().Policy);
    }

    [Fact]
    public async Task GetHistory_PlayerNoConsolation_ReturnsEmpty()
    {
        var game = CreateGameWithConsolation(out _);
        _gameRepo.ListAsync(Arg.Any<AllGamesWithPlayerSpecification>(), Arg.Any<CancellationToken>())
            .Returns(new List<Game> { game });

        var winnerId = game.Players.First(p => p.ParticipationStatus == PlayerParticipationStatus.Winner).UserId;
        var handler = new GetPlayerConsolationHistoryHandler(_gameRepo);
        var result = await handler.HandleAsync(
            new GetPlayerConsolationHistoryQuery(winnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Consolations);
    }

    private static Game CreateGameWithConsolation(out Guid loserId)
    {
        var categoryId = new CategoryId(Guid.NewGuid());
        var config = new GameConfiguration(
            "Test Game", categoryId, 5, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            ScoringSystem.Standard, LossPolicy.LoseAll,
            WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.FixedPoints,
            new RewardRules("Points", 1000), 2, 10, 100, 0, 0, 50);
        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        var winnerId = Guid.NewGuid();
        loserId = Guid.NewGuid();
        game.JoinPlayer(winnerId, "Winner");
        game.JoinPlayer(loserId, "Loser");
        game.Start();

        for (int i = 0; i < 5; i++)
        {
            var q = CreateQuestion(categoryId);
            game.StartRound(q.Id.Value, 1);
            game.SubmitAnswer(winnerId, q.AnswerOptions.First(o => o.IsCorrect).Id, DateTimeOffset.UtcNow, qid => qid == q.Id ? q : null);
            game.SubmitAnswer(loserId, q.AnswerOptions.First(o => !o.IsCorrect).Id, DateTimeOffset.UtcNow, qid => qid == q.Id ? q : null);
            game.CompleteRound(game.CurrentRound!.Id.Value);
        }

        game.Finish();
        return game;
    }

    private static Question CreateQuestion(CategoryId categoryId)
    {
        return Domain.Questions.Question.Create(
            "Test question?", categoryId,
            DifficultyLevel.Basic, AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            new (string text, bool isCorrect, int displayOrder)[]
            {
                ("Correct", true, 0), ("Wrong B", false, 1),
                ("Wrong C", false, 2), ("Wrong D", false, 3)
            }, Guid.NewGuid()).Value;
    }
}
