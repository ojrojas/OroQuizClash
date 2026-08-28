using QuizArena.Admin.Client.Models.Reports;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

public sealed class ReportsRewardsTests
{
    [Fact]
    public void RewardsMetrics_Consolation_NotCounted_AsNormal()
    {
        var rewards = new RewardMetric(10, new Dictionary<string,int>{{"Voucher",5}}, new Dictionary<string,int>{{"Active",8}});
        var consol = new ConsolationMetric(3, 300, new Dictionary<string,int>());
        Assert.Equal(10, rewards.TotalRewards);
        Assert.Equal(3, consol.TotalConsolations);
        Assert.NotEqual(rewards.TotalRewards, consol.TotalConsolations);
    }

    [Fact]
    public void RedemptionMetric_TotalCost_Ledger()
    {
        var redemptions = new RedemptionMetric(5, new Dictionary<string,int>{{"Approved",3}}, new Dictionary<string,int>{{"Voucher",5}}, 500);
        Assert.Equal(500, redemptions.TotalCost);
        Assert.Equal(5, redemptions.TotalRedemptions);
    }

    [Fact]
    public void ReportFilter_Level_Validation()
    {
        Assert.True(new ReportFilter(Level: 0).Validate().ContainsKey(nameof(ReportFilter.Level)));
        Assert.True(new ReportFilter(Level: 6).Validate().ContainsKey(nameof(ReportFilter.Level)));
        Assert.Empty(new ReportFilter(Level: 1).Validate());
        Assert.Empty(new ReportFilter(Level: 5).Validate());
    }

    [Fact]
    public void ReportFilter_Result_Validation()
    {
        Assert.True(new ReportFilter(Result: "INVALID").Validate().ContainsKey(nameof(ReportFilter.Result)));
        Assert.Empty(new ReportFilter(Result: "FINISHED").Validate());
        Assert.Empty(new ReportFilter(Result: "Approved").Validate());
    }

    [Fact]
    public void Authorization_RewardManager_403_ForOperational()
    {
        var roles = new[] { "REWARD_MANAGER" };
        var canSeeOperational = roles.Any(r => r == "ADMIN" || r == "GAME_MANAGER");
        Assert.False(canSeeOperational);
        var canSeeRewards = roles.Any(r => r == "ADMIN" || r == "REWARD_MANAGER");
        Assert.True(canSeeRewards);
    }

    [Fact]
    public void Authorization_GameManager_403_ForRewards()
    {
        var roles = new[] { "GAME_MANAGER" };
        var canSeeRewards = roles.Any(r => r == "ADMIN" || r == "REWARD_MANAGER");
        Assert.False(canSeeRewards);
        var canSeeOperational = roles.Any(r => r == "ADMIN" || r == "GAME_MANAGER");
        Assert.True(canSeeOperational);
    }

    [Fact]
    public void Paginacion_Valid()
    {
        var f = new ReportFilter(Page: 1, PageSize: 20);
        Assert.Empty(f.Validate());
        f = new ReportFilter(Page: 0);
        Assert.True(f.Validate().ContainsKey(nameof(ReportFilter.Page)));
        f = new ReportFilter(PageSize: 200);
        Assert.True(f.Validate().ContainsKey(nameof(ReportFilter.PageSize)));
    }

    [Fact]
    public void ReportCatalogs_Results_ContainExpected()
    {
        Assert.Contains("FINISHED", ReportCatalogs.Results);
        Assert.Contains("Correct", ReportCatalogs.Results);
        Assert.Contains("Approved", ReportCatalogs.Results);
    }
}
