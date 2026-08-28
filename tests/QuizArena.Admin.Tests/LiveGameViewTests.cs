using QuizArena.Admin.Client.Models.LiveGame;

namespace QuizArena.Admin.Tests;

public sealed class LiveGameViewTests
{
    [Fact]
    public void LiveGameView_HasTenIndicators()
    {
        var view = new LiveGameView(
            GameId: Guid.NewGuid(),
            Status: GameStateView.Running,
            CurrentRound: 1,
            CurrentQuestion: new QuestionView(Guid.NewGuid(), "Q?", new List<AnswerView> { new(Guid.NewGuid(), "A", 'A'), new(Guid.NewGuid(), "B", 'B'), new(Guid.NewGuid(), "C", 'C'), new(Guid.NewGuid(), "D", 'D') }, null),
            TotalRounds: 5,
            Players: 5,
            PlayersConnected: 3,
            PlayersAnswered: 2,
            PlayersWaiting: 1,
            Scores: new List<LiveScore> { new(Guid.NewGuid(), "Alice", 100, 0, 1, true) },
            CurrentLevel: 1,
            RemainingSeconds: 30,
            RowVersion: "abc",
            LastUpdated: DateTimeOffset.UtcNow);

        Assert.Equal(5, view.Players);
        Assert.Equal(3, view.PlayersConnected);
        Assert.Equal(2, view.PlayersAnswered);
        Assert.Equal(1, view.PlayersWaiting);
        Assert.Equal(3, view.PlayersAnswered + view.PlayersWaiting);
        Assert.Equal(view.PlayersConnected, view.PlayersAnswered + view.PlayersWaiting);
        Assert.Single(view.Scores);
    }

    [Fact]
    public void LiveGameView_ScoresReconstructedFromLedger()
    {
        var scores = new List<LiveScore>
        {
            new(Guid.NewGuid(), "Bob", 150, 50, 2, true),
            new(Guid.NewGuid(), "Alice", 100, 0, 1, false)
        };
        var view = new LiveGameView(Guid.NewGuid(), GameStateView.Running, 1, null, 5, 2, 2, 1, 1, scores, 1, 30, "v1", DateTimeOffset.UtcNow);
        Assert.Equal(2, view.Scores.Count);
        Assert.Equal(150, view.Scores.First().Score);
    }

    [Fact]
    public void GameStateViewMap_MapsCorrectly()
    {
        Assert.Equal(GameStateView.Running, GameStateViewMap.FromApi("IN_PROGRESS"));
        Assert.Equal(GameStateView.Paused, GameStateViewMap.FromApi("PAUSED"));
        Assert.Equal("Running", GameStateViewMap.ToDisplayName(GameStateView.Running));
    }
}
