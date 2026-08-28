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

public sealed class RealtimeRoundFlowBroadcastTests
{
    private static Game CreateGame()
    {
        var config = new GameConfiguration(
            "RoundFlow Game",
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
        game.JoinPlayer(Guid.NewGuid(), "Alice");
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

    private static IRepository<Game, GameId> GameRepo(Game? game)
    {
        var repo = Substitute.For<IRepository<Game, GameId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(game));
        return repo;
    }

    private static IRepository<Question, QuestionId> QuestionRepo(Question question)
    {
        var repo = Substitute.For<IRepository<Question, QuestionId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Question>>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Question?>(question));
        return repo;
    }

    [Fact]
    public async Task RoundStarted_BroadcastsRoundStartedAndQuestionPresented()
    {
        var game = CreateGame();
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var roundId = game.CurrentRound!.Id.Value;
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new RoundStartedBroadcastHandler(broadcaster, QuestionRepo(question), NullLogger<RoundStartedBroadcastHandler>.Instance);

        await handler.HandleAsync(new RoundStartedDomainEvent(game.Id.Value, roundId, 1, question.Id.Value), CancellationToken.None);

        await broadcaster.Received(1).RoundStartedAsync(game.Id.Value, roundId, 1, Arg.Any<CancellationToken>());
        await broadcaster.Received(1).QuestionPresentedAsync(
            game.Id.Value,
            roundId,
            1,
            Arg.Is<QuestionPresentedPayload>(p => p.QuestionId == question.Id.Value && p.AnswerOptions.Count == 4 && p.AnswerOptions.All(o => !string.IsNullOrEmpty(o.Text))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoundStarted_QuestionPresented_FiltersIsCorrect()
    {
        var game = CreateGame();
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var roundId = game.CurrentRound!.Id.Value;
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        QuestionPresentedPayload? captured = null;
        broadcaster.QuestionPresentedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Do<QuestionPresentedPayload>(p => captured = p), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var handler = new RoundStartedBroadcastHandler(broadcaster, QuestionRepo(question), NullLogger<RoundStartedBroadcastHandler>.Instance);

        await handler.HandleAsync(new RoundStartedDomainEvent(game.Id.Value, roundId, 1, question.Id.Value), CancellationToken.None);

        Assert.NotNull(captured);
        var json = System.Text.Json.JsonSerializer.Serialize(captured);
        Assert.DoesNotContain("IsCorrect", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RoundCompleted_BroadcastsRoundCompleted()
    {
        var game = CreateGame();
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var roundId = game.CurrentRound!.Id.Value;
        game.CompleteRound(roundId);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        var handler = new RoundCompletedBroadcastHandler(broadcaster, GameRepo(game), NullLogger<RoundCompletedBroadcastHandler>.Instance);

        await handler.HandleAsync(new RoundCompletedDomainEvent(game.Id.Value, roundId), CancellationToken.None);

        await broadcaster.Received(1).RoundCompletedAsync(game.Id.Value, roundId, 1, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RoundStarted_BroadcasterFailure_IsSwallowed()
    {
        var game = CreateGame();
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var roundId = game.CurrentRound!.Id.Value;
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        broadcaster.RoundStartedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("hub down")));
        var handler = new RoundStartedBroadcastHandler(broadcaster, QuestionRepo(question), NullLogger<RoundStartedBroadcastHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(new RoundStartedDomainEvent(game.Id.Value, roundId, 1, question.Id.Value), CancellationToken.None));
        Assert.Null(ex);
    }

    [Fact]
    public async Task RoundCompleted_BroadcasterFailure_IsSwallowed()
    {
        var game = CreateGame();
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var roundId = game.CurrentRound!.Id.Value;
        game.CompleteRound(roundId);
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        broadcaster.RoundCompletedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new InvalidOperationException("hub down")));
        var handler = new RoundCompletedBroadcastHandler(broadcaster, GameRepo(game), NullLogger<RoundCompletedBroadcastHandler>.Instance);

        var ex = await Record.ExceptionAsync(() => handler.HandleAsync(new RoundCompletedDomainEvent(game.Id.Value, roundId), CancellationToken.None));
        Assert.Null(ex);
    }
}
