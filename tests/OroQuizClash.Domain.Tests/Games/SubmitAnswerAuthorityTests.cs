using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class SubmitAnswerAuthorityTests
{
    private static Game CreateGame(out Question question, out Guid playerId)
    {
        var categoryId = new CategoryId(Guid.NewGuid());
        var config = new GameConfiguration("Auth Game", categoryId, 5, 10, 1, DifficultyProgressionStrategy.Linear, 30, ScoringSystem.Standard, LossPolicy.LoseCurrentRound, WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None, new RewardRules("Points", 1000), 2, 10, 100);
        var game = Game.Create(config, Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        playerId = Guid.NewGuid();
        game.JoinPlayer(playerId, "Alice");
        game.JoinPlayer(Guid.NewGuid(), "Bob");
        game.Start();
        question = Question.Create("Valid question text for test?", categoryId, DifficultyLevel.Basic, AcademicLevel.Create("Primary"), AgeRange.Create(6, 10), new (string text, bool isCorrect, int displayOrder)[] { ("Correct", true, 0), ("Wrong B", false, 1), ("Wrong C", false, 2), ("Wrong D", false, 3) }, Guid.NewGuid()).Value;
        game.StartRound(question.Id.Value, 1);
        return game;
    }

    [Fact]
    public void SubmitAnswer_IgnoresClientScore_UsesServerEvaluation()
    {
        var game = CreateGame(out var question, out var playerId);
        var correctOption = question.AnswerOptions.First(o => o.IsCorrect).Id;
        var result = game.SubmitAnswer(playerId, correctOption, DateTimeOffset.UtcNow, _ => question);
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Correct == true);
        Assert.True(result.Value.Points > 0);
    }

    [Fact]
    public void SubmitAnswer_UsesServerTime_NotClientTime()
    {
        var game = CreateGame(out var question, out var playerId);
        var correctOption = question.AnswerOptions.First(o => o.IsCorrect).Id;
        var result = game.SubmitAnswer(playerId, correctOption, DateTimeOffset.UtcNow, _ => question);
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ElapsedTime >= 0);
        Assert.True(result.Value.ElapsedTime < 5000);
    }

    [Fact]
    public void SubmitAnswer_RejectsInvalidAnswerOption()
    {
        var game = CreateGame(out var question, out var playerId);
        var invalidOption = new AnswerOptionId(Guid.NewGuid());
        var result = game.SubmitAnswer(playerId, invalidOption, DateTimeOffset.UtcNow, _ => question);
        Assert.True(result.IsFailure);
    }
}
