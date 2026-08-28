using OroQuizClash.Domain.Games;

namespace OroQuizClash.Application.Features.Games;

public static class LeaderboardBuilder
{
    public static IReadOnlyList<LeaderboardEntryResponse> Build(Game game) =>
        game.Players
            .Select(p =>
            {
                var playerAnswers = game.Answers.Where(a => a.PlayerId == p.UserId).ToList();
                var correctAnswers = playerAnswers.Count(a => a.Correct == true);
                var currentLevel = game.Rounds
                    .FirstOrDefault(r => r.RoundNumber == p.CurrentRoundNumber)?.Difficulty;
                return (Player: p, CorrectAnswers: correctAnswers, CurrentLevel: currentLevel, AchievedAt: FirstAchievedAt(game, p));
            })
            // Deterministic ordering (SPEC-011 FR-011): points desc -> correct answers desc
            // -> earliest achievement of current balance -> join order.
            .OrderByDescending(x => x.Player.Score.CurrentPoints)
            .ThenByDescending(x => x.CorrectAnswers)
            .ThenBy(x => x.AchievedAt)
            .ThenBy(x => x.Player.JoinedAt)
            .Select((x, index) => new LeaderboardEntryResponse(
                x.Player.UserId,
                x.Player.DisplayName,
                index + 1,
                x.Player.Score.CurrentPoints,
                x.CorrectAnswers,
                x.CurrentLevel,
                x.Player.ParticipationStatus.Name,
                x.Player.Score.SecuredPoints))
            .ToList();

    private static DateTimeOffset FirstAchievedAt(Game game, GamePlayer player)
    {
        var currentPoints = player.Score.CurrentPoints;
        var transaction = game.PointTransactions
            .Where(pt => pt.PlayerId == player.UserId && pt.ResultingBalance == currentPoints)
            .OrderBy(pt => pt.CreatedAt)
            .FirstOrDefault();
        return transaction?.CreatedAt ?? player.JoinedAt;
    }
}
