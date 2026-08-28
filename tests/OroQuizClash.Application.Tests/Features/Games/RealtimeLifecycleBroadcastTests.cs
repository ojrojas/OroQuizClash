using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Specifications;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

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

public sealed class RealtimeLifecycleBroadcastTests
{
    private static Game CreateGame(out Guid playerA, out Guid playerB)
    {
        var config = new GameConfiguration(
            "Lifecycle Game",
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
        game.JoinPlayer(playerA, "Alice");
        game.JoinPlayer(playerB, "Bob");
        game.Start();
        return game;
    }

    private static IRepository<Game, GameId> Repo(Game? game)
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(game));
        return repo;
    }

    private static Question CreateQuestion(CategoryId categoryId) =>
        Question.Create(
            "Test question?",
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

    [Fact]
    public async Task GameStarted_BroadcastsGameStarted()
    {
        var game = CreateGame(out _, out _);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new GameStartedBroadcastHandler(broadcaster, NullLogger<GameStartedBroadcastHandler>.Instance);

        await handler.HandleAsync(new GameStartedDomainEvent(game.Id.Value), CancellationToken.None);

        await broadcaster.Received(1).GameStartedAsync(game.Id.Value, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayerJoined_BroadcastsWithDisplayName()
    {
        var game = CreateGame(out var playerA, out _);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new PlayerJoinedBroadcastHandler(broadcaster, Repo(game), NullLogger<PlayerJoinedBroadcastHandler>.Instance);

        await handler.HandleAsync(new PlayerJoinedDomainEvent(game.Id.Value, playerA), CancellationToken.None);

        await broadcaster.Received(1).PlayerJoinedAsync(game.Id.Value, playerA, "Alice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GameFinished_BroadcastsGameFinishedWithLeaderboard()
    {
        var game = CreateGame(out var playerA, out _);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        game.SubmitAnswer(playerA, option, DateTimeOffset.UtcNow, qid => question);
        game.CompleteRound(game.CurrentRound!.Id.Value);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new GameFinishedBroadcastHandler(broadcaster, Repo(game), NullLogger<GameFinishedBroadcastHandler>.Instance);

        await handler.HandleAsync(new GameFinishedDomainEvent(game.Id.Value), CancellationToken.None);

        await broadcaster.Received(1).GameFinishedAsync(game.Id.Value, "FINISHED", Arg.Is<IReadOnlyList<LeaderboardEntryResponse>>(l => l.Count == 2), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GameForcedFinished_AlsoBroadcastsGameFinished()
    {
        var game = CreateGame(out _, out _);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new GameFinishedBroadcastHandler(broadcaster, Repo(game), NullLogger<GameFinishedBroadcastHandler>.Instance);

        await handler.HandleAsync(new GameForcedFinishedDomainEvent(game.Id.Value, "timeout"), CancellationToken.None);

        await broadcaster.Received(1).GameFinishedAsync(game.Id.Value, "FORCED_FINISHED", Arg.Any<IReadOnlyList<LeaderboardEntryResponse>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GameCancelled_AlsoBroadcastsGameFinished()
    {
        var game = CreateGame(out _, out _);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new GameFinishedBroadcastHandler(broadcaster, Repo(game), NullLogger<GameFinishedBroadcastHandler>.Instance);

        await handler.HandleAsync(new GameCancelledDomainEvent(game.Id.Value, "cancel"), CancellationToken.None);

        await broadcaster.Received(1).GameFinishedAsync(game.Id.Value, "CANCELLED", Arg.Any<IReadOnlyList<LeaderboardEntryResponse>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GameStarted_BroadcasterFailure_IsSwallowed()
    {
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        broadcaster.GameStartedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(Task.FromException(new InvalidOperationException("hub down")));
        var handler = new GameStartedBroadcastHandler(broadcaster, NullLogger<GameStartedBroadcastHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(new GameStartedDomainEvent(Guid.NewGuid()), CancellationToken.None));
        Assert.Null(ex);
    }
}
