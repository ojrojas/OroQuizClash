using QuizArena.Admin.Client.Models.Reports;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

public sealed class ReportsOperationalTests
{
    [Fact]
    public void ReportFilter_FromAfterTo_Fails()
    {
        var f = new ReportFilter(From: new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero), To: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(f.Validate().ContainsKey("DateRange"));
    }

    [Fact]
    public void ReportFilter_Level_OutOfRange_Fails()
    {
        var f = new ReportFilter(Level: 0);
        Assert.True(f.Validate().ContainsKey(nameof(ReportFilter.Level)));
        f = new ReportFilter(Level: 6);
        Assert.True(f.Validate().ContainsKey(nameof(ReportFilter.Level)));
    }

    [Fact]
    public void ReportFilter_Level_Valid_Pass()
    {
        var f = new ReportFilter(Level: 3);
        Assert.Empty(f.Validate());
    }

    [Fact]
    public void ReportFilter_Result_Invalid_Fails()
    {
        var f = new ReportFilter(Result: "INVALID_RESULT_XYZ");
        Assert.True(f.Validate().ContainsKey(nameof(ReportFilter.Result)));
    }

    [Fact]
    public void ReportFilter_Valid_Pass()
    {
        var f = new ReportFilter(From: new DateTimeOffset(2026, 4, 1, 0, 0, 0, TimeSpan.Zero), To: new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero), Level: 3, Result: "FINISHED");
        Assert.Empty(f.Validate());
    }

    [Fact]
    public void ReportCatalogs_GameStatuses_Nine()
    {
        Assert.Equal(9, ReportCatalogs.GameStatuses.Count);
        Assert.Contains("FINISHED", ReportCatalogs.GameStatuses);
    }

    [Fact]
    public void ReportCatalogs_Level_Five()
    {
        Assert.Equal(5, ReportCatalogs.Levels.Count);
        Assert.Contains(3, ReportCatalogs.Levels);
    }

    [Fact]
    public void ReportSnapshot_CalculatedAt_Present()
    {
        var filter = new ReportFilter();
        var operational = new OperationalMetrics(new GameMetric(10, new Dictionary<string,int>{{"FINISHED",5}}), new PlayerMetric(5,3,new Dictionary<string,int>()), new QuestionMetric(100,new Dictionary<string,int>(),new Dictionary<int,int>()), new CategoryMetric(5,3,new Dictionary<string,int>()));
        var performance = new PerformanceMetrics(new AnswerMetric(100,60,40,0.6), new ScoreMetric(1000,245.5,new Dictionary<string,int>(),new Dictionary<string,int>()), new WithdrawalMetric(5,new Dictionary<string,int>(),0.1));
        var rewards = new RewardsMetrics(new RewardMetric(10,new Dictionary<string,int>(),new Dictionary<string,int>()), new RedemptionMetric(5,new Dictionary<string,int>(),new Dictionary<string,int>(),500), new ConsolationMetric(2,200,new Dictionary<string,int>()));
        var snapshot = new ReportSnapshot(filter, operational, performance, rewards, 10, DateTimeOffset.UtcNow);
        Assert.True(snapshot.CalculatedAt <= DateTimeOffset.UtcNow);
        Assert.Equal(10, snapshot.Operational.Games.TotalGames);
        Assert.Equal(0.6, snapshot.Performance.Answers.AccuracyRate);
    }

    [Fact]
    public void IsConsolation_Separated()
    {
        var consol = new ConsolationMetric(5, 500, new Dictionary<string,int>{{"eligible",3}});
        var normal = new RewardMetric(10, new Dictionary<string,int>{{"Voucher",5}}, new Dictionary<string,int>());
        Assert.NotEqual(consol.TotalConsolations, normal.TotalRewards);
        Assert.Equal(5, consol.TotalConsolations);
    }
}
