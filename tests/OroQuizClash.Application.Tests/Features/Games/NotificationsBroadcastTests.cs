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

public sealed class NotificationsBroadcastTests
{
    private static Game CreateGame(out Guid playerA, out Guid playerB)
    {
        var config = new GameConfiguration(
            "Broadcast Game",
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
    public async Task PlayerJoined_BroadcastsWithDisplayName()
    {
        var game = CreateGame(out var playerA, out _);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new PlayerJoinedBroadcastHandler(broadcaster, RepoReturning(game), NullLogger<PlayerJoinedBroadcastHandler>.Instance);

        await handler.HandleAsync(new PlayerJoinedDomainEvent(game.Id.Value, playerA), CancellationToken.None);

        await broadcaster.Received(1).PlayerJoinedAsync(game.Id.Value, playerA, "Alice", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScoreUpdated_MapsEventFields()
    {
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new ScoreUpdatedBroadcastHandler(broadcaster, NullLogger<ScoreUpdatedBroadcastHandler>.Instance);
        var (gameId, playerId) = (Guid.NewGuid(), Guid.NewGuid());

        await handler.HandleAsync(
            new ScoreUpdatedDomainEvent(gameId, playerId, 100, 250, "RoundBonus"), CancellationToken.None);

        await broadcaster.Received(1).ScoreUpdatedAsync(gameId, playerId, 100, 250, "RoundBonus", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnswerEvaluated_BroadcastsRebuiltLeaderboard()
    {
        var game = CreateGame(out var playerA, out _);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        var answer = game.SubmitAnswer(playerA, option, DateTimeOffset.UtcNow, qid => question).Value;
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new LeaderboardBroadcastHandler(broadcaster, RepoReturning(game), NullLogger<LeaderboardBroadcastHandler>.Instance);

        await handler.HandleAsync(
            new AnswerEvaluatedDomainEvent(game.Id.Value, answer.Id.Value, playerA, game.CurrentRound!.Id.Value, true, 100, 5, AnswerStatus.Evaluated),
            CancellationToken.None);

        await broadcaster.Received(1).LeaderboardUpdatedAsync(
            game.Id.Value,
            Arg.Is<IReadOnlyList<LeaderboardEntryResponse>>(e =>
                e.Count == 2 && e[0].PlayerId == playerA && e[0].Rank == 1 && e[0].Points == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoundCompleted_BroadcastsLeaderboard()
    {
        var game = CreateGame(out _, out _);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var roundId = game.CurrentRound!.Id.Value;
        game.CompleteRound(roundId);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new LeaderboardBroadcastHandler(broadcaster, RepoReturning(game), NullLogger<LeaderboardBroadcastHandler>.Instance);

        await handler.HandleAsync(new RoundCompletedDomainEvent(game.Id.Value, roundId), CancellationToken.None);

        await broadcaster.Received(1).LeaderboardUpdatedAsync(
            game.Id.Value,
            Arg.Is<IReadOnlyList<LeaderboardEntryResponse>>(e => e.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Leaderboard_GameNotFound_DoesNotBroadcast()
    {
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new LeaderboardBroadcastHandler(broadcaster, RepoReturning(null), NullLogger<LeaderboardBroadcastHandler>.Instance);

        await handler.HandleAsync(new RoundCompletedDomainEvent(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        await broadcaster.DidNotReceiveWithAnyArgs().LeaderboardUpdatedAsync(default, default!, default);
    }

    [Fact]
    public async Task PlayerWithdrawn_BroadcastsWithdrawnStatusAndRetainedPoints()
    {
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new PlayerStatusBroadcastHandler(broadcaster, RepoReturning(null), NullLogger<PlayerStatusBroadcastHandler>.Instance);
        var (gameId, playerId) = (Guid.NewGuid(), Guid.NewGuid());

        await handler.HandleAsync(
            new PlayerWithdrawnDomainEvent(gameId, playerId, 150, "KeepCurrentScore"), CancellationToken.None);

        await broadcaster.Received(1).PlayerStatusChangedAsync(gameId, playerId, "WITHDRAWN", 150, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task PlayerEliminated_BroadcastsEliminatedStatusWithFinalScore()
    {
        var game = CreateGame(out var playerA, out _);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        game.SubmitAnswer(playerA, option, DateTimeOffset.UtcNow, qid => question);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new PlayerStatusBroadcastHandler(broadcaster, RepoReturning(game), NullLogger<PlayerStatusBroadcastHandler>.Instance);

        await handler.HandleAsync(
            new PlayerEliminatedDomainEvent(game.Id.Value, playerA, "Loss policy"), CancellationToken.None);

        await broadcaster.Received(1).PlayerStatusChangedAsync(game.Id.Value, playerA, "ELIMINATED", 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GameFinished_BroadcastsWinnerAndFinishedPerPlayer()
    {
        var game = CreateGame(out var playerA, out var playerB);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        game.SubmitAnswer(playerA, option, DateTimeOffset.UtcNow, qid => question);
        game.CompleteRound(game.CurrentRound!.Id.Value);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new PlayerStatusBroadcastHandler(broadcaster, RepoReturning(game), NullLogger<PlayerStatusBroadcastHandler>.Instance);

        await handler.HandleAsync(new GameFinishedDomainEvent(game.Id.Value), CancellationToken.None);

        await broadcaster.Received(1).PlayerStatusChangedAsync(game.Id.Value, playerA, "WINNER", 100, Arg.Any<CancellationToken>());
        await broadcaster.Received(1).PlayerStatusChangedAsync(game.Id.Value, playerB, "FINISHED", 0, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BroadcasterFailure_IsSwallowed()
    {
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        broadcaster.ScoreUpdatedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("hub down"));
        var handler = new ScoreUpdatedBroadcastHandler(broadcaster, NullLogger<ScoreUpdatedBroadcastHandler>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            handler.HandleAsync(new ScoreUpdatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), 1, 1, "T"), CancellationToken.None));

        Assert.Null(exception);
    }

    [Fact]
    public async Task BroadcasterFailure_OnLeaderboard_IsSwallowed()
    {
        var game = CreateGame(out _, out _);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        broadcaster.LeaderboardUpdatedAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<LeaderboardEntryResponse>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("hub down"));
        var handler = new LeaderboardBroadcastHandler(broadcaster, RepoReturning(game), NullLogger<LeaderboardBroadcastHandler>.Instance);

        var exception = await Record.ExceptionAsync(() =>
            handler.HandleAsync(new RoundCompletedDomainEvent(game.Id.Value, Guid.NewGuid()), CancellationToken.None));

        Assert.Null(exception);
    }
}
