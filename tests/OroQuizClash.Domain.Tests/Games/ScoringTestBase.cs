using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

using Game = OroQuizClash.Domain.Games.Game;
using GameRound = OroQuizClash.Domain.Games.GameRound;

namespace OroQuizClash.Domain.Tests.Games;

public static class ScoringTestBase
{
    public static GameConfiguration Config(
        LossPolicy? lossPolicy = null,
        WithdrawalPolicy? withdrawalPolicy = null,
        ConsolationPolicy? consolationPolicy = null,
        ScoringSystem? scoringSystem = null,
        int pointsPerRound = 100,
        int minRounds = 5,
        int minPlayers = 2)
    {
        return new GameConfiguration(
            "Scoring Game",
            new CategoryId(Guid.NewGuid()),
            minRounds, 10, 1,
            DifficultyProgressionStrategy.Linear, 30,
            scoringSystem ?? ScoringSystem.Standard,
            lossPolicy ?? LossPolicy.LoseAll,
            withdrawalPolicy ?? WithdrawalPolicy.KeepCurrentScore,
            consolationPolicy ?? ConsolationPolicy.None,
            new RewardRules("Points", 1000),
            minPlayers, 10,
            pointsPerRound);
    }

    public static Game CreateStartedGame(
        out Guid player1,
        out Guid player2,
        GameConfiguration? config = null)
    {
        var game = Game.Create(config ?? Config(), Guid.NewGuid()).Value;
        game.MarkReady(_ => true, _ => 5);
        game.OpenLobby();
        player1 = Guid.NewGuid();
        player2 = Guid.NewGuid();
        game.JoinPlayer(player1, "Alice");
        game.JoinPlayer(player2, "Bob");
        game.Start();
        return game;
    }

    public static Question CreateQuestion(CategoryId categoryId)
    {
        return Domain.Questions.Question.Create(
            "Test question?",
            categoryId,
            DifficultyLevel.Basic,
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
    }

    public static GameRound StartRoundWithQuestion(Game game, Question question, int difficulty = 1)
    {
        var result = game.StartRound(question.Id.Value, difficulty);
        return result.Value;
    }

    public static AnswerOptionId CorrectOption(Question question) =>
        question.AnswerOptions.First(o => o.IsCorrect).Id;

    public static AnswerOptionId IncorrectOption(Question question) =>
        question.AnswerOptions.First(o => !o.IsCorrect).Id;

    public static Func<QuestionId, Question?> Resolver(Question question) =>
        qid => qid == question.Id ? question : null;
}
