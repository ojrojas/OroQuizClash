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

public sealed class RealtimeResilienceTests
{
    private static Game CreateGame()
    {
        var config = new GameConfiguration(
            "Resilience Game",
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
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Game>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(game));
        return repo;
    }

    private static IRepository<Question, QuestionId> QuestionRepo(Question q)
    {
        var repo = Substitute.For<IRepository<Question, QuestionId>>();
        repo.FirstOrDefaultAsync(Arg.Any<ISpecification<Question>>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult<Question?>(q));
        return repo;
    }

    [Theory]
    [InlineData("GameStarted")]
    [InlineData("RoundStarted")]
    [InlineData("PlayerAnswered")]
    [InlineData("RoundCompleted")]
    [InlineData("GameFinished")]
    public async Task AllHandlers_SwallowBroadcastFailures(string eventType)
    {
        var game = CreateGame();
        var question = CreateQuestion(game.Configuration.CategoryId);
        game.StartRound(question.Id.Value, 1);
        var roundId = game.CurrentRound!.Id.Value;
        var broadcaster = Substitute.For<IGameNotificationsBroadcaster>();
        broadcaster.When(x => x.GameStartedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())).Do(_ => throw new InvalidOperationException("hub down"));
        broadcaster.When(x => x.RoundStartedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())).Do(_ => throw new InvalidOperationException("hub down"));
        broadcaster.When(x => x.QuestionPresentedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<QuestionPresentedPayload>(), Arg.Any<CancellationToken>())).Do(_ => throw new InvalidOperationException("hub down"));
        broadcaster.When(x => x.PlayerAnsweredAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())).Do(_ => throw new InvalidOperationException("hub down"));
        broadcaster.When(x => x.RoundCompletedAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CancellationToken>())).Do(_ => throw new InvalidOperationException("hub down"));
        broadcaster.When(x => x.GameFinishedAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<LeaderboardEntryResponse>>(), Arg.Any<CancellationToken>())).Do(_ => throw new InvalidOperationException("hub down"));

        Exception? ex = null;
        switch (eventType)
        {
            case "GameStarted":
                ex = await Record.ExceptionAsync(() => new GameStartedBroadcastHandler(broadcaster, NullLogger<GameStartedBroadcastHandler>.Instance).HandleAsync(new GameStartedDomainEvent(game.Id.Value), CancellationToken.None));
                break;
            case "RoundStarted":
                ex = await Record.ExceptionAsync(() => new RoundStartedBroadcastHandler(broadcaster, QuestionRepo(question), NullLogger<RoundStartedBroadcastHandler>.Instance).HandleAsync(new RoundStartedDomainEvent(game.Id.Value, roundId, 1, question.Id.Value), CancellationToken.None));
                break;
            case "PlayerAnswered":
                ex = await Record.ExceptionAsync(() => new PlayerAnsweredBroadcastHandler(broadcaster, NullLogger<PlayerAnsweredBroadcastHandler>.Instance).HandleAsync(new AnswerSubmittedDomainEvent(game.Id.Value, Guid.NewGuid(), Guid.NewGuid(), roundId, question.Id.Value, Guid.NewGuid()), CancellationToken.None));
                break;
            case "RoundCompleted":
                game.CompleteRound(roundId);
                ex = await Record.ExceptionAsync(() => new RoundCompletedBroadcastHandler(broadcaster, GameRepo(game), NullLogger<RoundCompletedBroadcastHandler>.Instance).HandleAsync(new RoundCompletedDomainEvent(game.Id.Value, roundId), CancellationToken.None));
                break;
            case "GameFinished":
                ex = await Record.ExceptionAsync(() => new GameFinishedBroadcastHandler(broadcaster, GameRepo(game), NullLogger<GameFinishedBroadcastHandler>.Instance).HandleAsync(new GameFinishedDomainEvent(game.Id.Value), CancellationToken.None));
                break;
        }
        Assert.Null(ex);
    }
}
