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

public sealed class RealtimeSourceOfTruthTests
{
    private static Game CreateGame(out Guid playerA)
    {
        var config = new GameConfiguration(
            "SourceTruth Game",
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
    public async Task LeaderboardUpdated_MatchesRestLeaderboard()
    {
        var game = CreateGame(out var playerA);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        game.SubmitAnswer(playerA, option, DateTimeOffset.UtcNow, qid => question);
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<Game?>(game));
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        IReadOnlyList<LeaderboardEntryResponse>? captured = null;
        broadcaster.LeaderboardUpdatedAsync(Arg.Any<Guid>(), Arg.Do<IReadOnlyList<LeaderboardEntryResponse>>(e => captured = e), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var handler = new LeaderboardBroadcastHandler(broadcaster, repo, NullLogger<LeaderboardBroadcastHandler>.Instance);

        await handler.HandleAsync(new AnswerEvaluatedDomainEvent(game.Id.Value, Guid.NewGuid(), playerA, game.CurrentRound!.Id.Value, true, 100, 5, AnswerStatus.Evaluated), CancellationToken.None);

        var expected = LeaderboardBuilder.Build(game);
        Assert.NotNull(captured);
        Assert.Equal(expected.Count, captured.Count);
        Assert.Equal(expected[0].Points, captured[0].Points);
        Assert.Equal(expected[0].PlayerId, captured[0].PlayerId);
    }

    [Fact]
    public async Task GameFinished_MatchesRestLeaderboard()
    {
        var game = CreateGame(out var playerA);
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        game.SubmitAnswer(playerA, option, DateTimeOffset.UtcNow, qid => question);
        game.CompleteRound(game.CurrentRound!.Id.Value);
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<Game?>(game));
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        IReadOnlyList<LeaderboardEntryResponse>? captured = null;
        broadcaster.GameFinishedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Do<IReadOnlyList<LeaderboardEntryResponse>>(e => captured = e), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var handler = new GameFinishedBroadcastHandler(broadcaster, repo, NullLogger<GameFinishedBroadcastHandler>.Instance);

        await handler.HandleAsync(new GameFinishedDomainEvent(game.Id.Value), CancellationToken.None);

        var expected = LeaderboardBuilder.Build(game);
        Assert.NotNull(captured);
        Assert.Equal(expected.Count, captured.Count);
    }
}
