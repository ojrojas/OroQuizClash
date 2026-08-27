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

public sealed class ConsolidationStatusHandlerTests
{
    private readonly IRepository<Game, GameId> _gameRepo = Substitute.For<IRepository<Game, GameId>>();

    [Fact]
    public async Task GetStatus_PlayerReceivedConsolation_ReturnsReceived()
    {
        var game = CreateGameWithConsolation(out var loser);
        _gameRepo.FirstOrDefaultAsync(Arg.Any<GameByIdWithAnswersSpecification>(), Arg.Any<CancellationToken>())
            .Returns(game);

        var handler = new GetPlayerConsolationStatusHandler(_gameRepo);
        var result = await handler.HandleAsync(
            new GetPlayerConsolationStatusQuery(game.Id.Value, loser),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Received);
        Assert.Equal("FixedPoints", result.Value.Policy);
    }

    [Fact]
    public async Task GetStatus_PlayerDidNotReceive_ReturnsNotReceived()
    {
        var game = CreateGameWithConsolation(out var loserId);
        var winnerId = game.Players.First(p => p.UserId != loserId).UserId;
        _gameRepo.FirstOrDefaultAsync(Arg.Any<GameByIdWithAnswersSpecification>(), Arg.Any<CancellationToken>())
            .Returns(game);

        var handler = new GetPlayerConsolationStatusHandler(_gameRepo);
        var result = await handler.HandleAsync(
            new GetPlayerConsolationStatusQuery(game.Id.Value, winnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Received);
    }

    [Fact]
    public async Task GetStatus_GameNotFound_ReturnsFailure()
    {
        _gameRepo.FirstOrDefaultAsync(Arg.Any<GameByIdWithAnswersSpecification>(), Arg.Any<CancellationToken>())
            .Returns((Game?)null);

        var handler = new GetPlayerConsolationStatusHandler(_gameRepo);
        var result = await handler.HandleAsync(
            new GetPlayerConsolationStatusQuery(Guid.NewGuid(), Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
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
