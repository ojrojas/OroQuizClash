namespace OroQuizClash.Application.Features.Reporting;

public static class ReportingMappers
{
    public static double? Accuracy(int correct, int answered)
    {
        if (answered == 0) return null;
        return Math.Round((double)correct / answered * 100, 2);
    }

    public static double? AverageResponseTime(IEnumerable<int> elapsedTimes)
    {
        var list = elapsedTimes.Where(t => t >= 0).ToList();
        if (list.Count == 0) return null;
        return Math.Round(list.Average(), 2);
    }

    public static Guid? WinnerId(IReadOnlyList<OroQuizClash.Application.Features.Games.LeaderboardEntryResponse> leaderboard)
    {
        if (leaderboard.Count == 0) return null;
        return leaderboard[0].PlayerId;
    }
}
