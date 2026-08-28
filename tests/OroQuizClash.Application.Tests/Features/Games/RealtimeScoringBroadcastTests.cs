using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Specifications;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;
using NSubstitute.ExceptionExtensions;

using OroQuizClash.Application.Features.Games;
using OroQuizClash.Application.Features.Games.Notifications;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.Events;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class RealtimeScoringBroadcastTests
{
    private static Game CreateGame(out Guid playerA)
    {
        var config = new GameConfiguration(
            "Scoring Game",
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
        game.JoinPlayer(playerA, "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        return game;
    }

    private static Question CreateQuestion(CategoryId categoryId) =>
        Question.Create(
            "Test?",
            categoryId,
            DifficultyLevel.Basic,
            AcademicLevel.Create("Primary"),
            AgeRange.Create(6, 10),
            new (string text, bool isCorrect, int displayOrder)[]
            {
                ("Correct", true, 0),
                ("Wrong B", false, 1),
                ("Wrong C", false, 2),
                ("Wrong D", false, 3)
            },
            Guid.NewGuid()).Value;

    private static IRepository<Game, GameId> Repo(Game? game)
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(game));
        return repo;
    }

    [Fact]
    public async Task AnswerSubmitted_BroadcastsPlayerAnsweredWithoutForbiddenFields()
    {
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new PlayerAnsweredBroadcastHandler(broadcaster, NullLogger<PlayerAnsweredBroadcastHandler>.Instance);
        var (gameId, playerId, roundId) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await handler.HandleAsync(new AnswerSubmittedDomainEvent(gameId, Guid.NewGuid(), playerId, roundId, Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await broadcaster.Received(1).PlayerAnsweredAsync(gameId, playerId, roundId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScoreUpdated_MapsToBroadcaster()
    {
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new ScoreUpdatedBroadcastHandler(broadcaster, NullLogger<ScoreUpdatedBroadcastHandler>.Instance);
        var (gameId, playerId) = (Guid.NewGuid(), Guid.NewGuid());

        await handler.HandleAsync(new ScoreUpdatedDomainEvent(gameId, playerId, 100, 250, "RoundBonus"), CancellationToken.None);

        await broadcaster.Received(1).ScoreUpdatedAsync(gameId, playerId, 100, 250, "RoundBonus", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswerEvaluated_BroadcastsLeaderboardSnapshot()
    {
        var game = CreateGame(out var playerA);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        var answer = game.SubmitAnswer(playerA, option, DateTimeOffset.UtcNow, qid => question).Value;
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new LeaderboardBroadcastHandler(broadcaster, Repo(game), NullLogger<LeaderboardBroadcastHandler>.Instance);

        await handler.HandleAsync(new AnswerEvaluatedDomainEvent(game.Id.Value, answer.Id.Value, playerA, game.CurrentRound!.Id.Value, true, 100, 5, AnswerStatus.Evaluated), CancellationToken.None);

        await broadcaster.Received(1).LeaderboardUpdatedAsync(game.Id.Value, Arg.Is<IReadOnlyList<LeaderboardEntryResponse>>(l => l.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayerAnswered_BroadcasterFailure_IsSwallowed()
    {
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        broadcaster.PlayerAnsweredAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("hub down"));
        var handler = new PlayerAnsweredBroadcastHandler(broadcaster, NullLogger<PlayerAnsweredBroadcastHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(new AnswerSubmittedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None));
        Assert.Null(ex);
    }

    [Fact]
    public async Task Leaderboard_BroadcasterFailure_IsSwallowed()
    {
        var game = CreateGame(out _);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        broadcaster.LeaderboardUpdatedAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<LeaderboardEntryResponse>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("hub down"));
        var handler = new LeaderboardBroadcastHandler(broadcaster, Repo(game), NullLogger<LeaderboardBroadcastHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(new AnswerEvaluatedDomainEvent(game.Id.Value, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), true, 100, 5, AnswerStatus.Evaluated), CancellationToken.None));
        Assert.Null(ex);
    }
}
