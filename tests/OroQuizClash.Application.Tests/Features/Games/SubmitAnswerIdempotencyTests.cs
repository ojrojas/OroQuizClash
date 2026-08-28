using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Application.Tests.Features.Games;

public sealed class SubmitAnswerIdempotencyTests
{
    private static (Game game, Question question, Guid playerId) Setup()
    {
        var categoryId = new CategoryId(Guid.NewGuid());
        var config = new GameConfiguration("Idem Game", categoryId, 5, 10, 1, DifficultyProgressionStrategy.Linear, 30, ScoringSystem.Standard, LossPolicy.LoseCurrentRound, WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None, new RewardRules("Points", 1000), 2, 10, 100);
        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        var playerId = Guid.NewGuid();
        game.JoinPlayer(playerId, "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        var question = Question.Create("Valid question for idempotency?", categoryId, DifficultyLevel.Basic, AcademicLevel.Create("Primary"), AgeRange.Create(6, 10), new (string text, bool isCorrect, int displayOrder)[] { ("Correct", true, 0), ("Wrong B", false, 1), ("Wrong C", false, 2), ("Wrong D", false, 3) }, Guid.NewGuid()).Value;
        game.StartRound(question.Id.Value, 1);
        return (game, question, playerId);
    }

    [Fact]
    public void SecondSubmit_SamePlayerRound_ReturnsOriginalWithoutDuplicatePoints()
    {
        var (game, question, playerId) = Setup();
        var option = question.AnswerOptions.First(o => o.IsCorrect).Id;
        var first = game.SubmitAnswer(playerId, option, DateTimeOffset.UtcNow, _ => question);
        var second = game.SubmitAnswer(playerId, option, DateTimeOffset.UtcNow, _ => question);
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.Id, second.Value.Id);
        Assert.Equal(1, game.Answers.Count(a => a.PlayerId == playerId));
    }
}
