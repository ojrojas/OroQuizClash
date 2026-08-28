using QuizArena.Admin.Client.Models.Dashboard;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

/// <summary>
/// T033: Dashboard matrix 7 shortcuts × 3 roles + 10 drill-down routes per contracts/navigation-map.md
/// and spec FR-011/014 SC-010.
/// </summary>
public sealed class DashboardAuthorizationTests
{
    [Theory]
    [InlineData("ADMIN", 7)]
    [InlineData("GAME_MANAGER", 6)]
    [InlineData("REWARD_MANAGER", 2)]
    public void QuickActions_ForRoles_CountMatchesSpec(string role, int expected)
    {
        var visible = QuickActionsCatalog.ForRoles([role]);
        Assert.Equal(expected, visible.Count);
    }

    [Fact]
    public void QuickActions_Admin_SeesAllSeven()
    {
        var all = QuickActionsCatalog.ForRoles(["ADMIN"]);
        Assert.Equal(QuickActionsCatalog.All.Count, all.Count);
        Assert.Contains(all, a => a.Id == QuickActionId.CreateGame);
        Assert.Contains(all, a => a.Id == QuickActionId.ManageRewards);
        Assert.Contains(all, a => a.Id == QuickActionId.ViewReports);
    }

    [Fact]
    public void QuickActions_GameManager_ExcludesManageRewards()
    {
        var visible = QuickActionsCatalog.ForRoles(["GAME_MANAGER"]);
        Assert.DoesNotContain(visible, a => a.Id == QuickActionId.ManageRewards);
        Assert.Contains(visible, a => a.Id == QuickActionId.CreateGame);
        Assert.Contains(visible, a => a.Id == QuickActionId.ViewReports);
    }

    [Fact]
    public void QuickActions_RewardManager_OnlyManageRewardsAndReports()
    {
        var visible = QuickActionsCatalog.ForRoles(["REWARD_MANAGER"]);
        Assert.Equal(2, visible.Count);
        Assert.Contains(visible, a => a.Id == QuickActionId.ManageRewards);
        Assert.Contains(visible, a => a.Id == QuickActionId.ViewReports);
    }

    [Fact]
    public void QuickActions_NoRoles_Empty()
    {
        Assert.Empty(QuickActionsCatalog.ForRoles([]));
    }

    [Theory]
    [InlineData(QuickActionId.CreateGame, "ADMIN", true)]
    [InlineData(QuickActionId.CreateGame, "REWARD_MANAGER", false)]
    [InlineData(QuickActionId.ManageRewards, "GAME_MANAGER", false)]
    [InlineData(QuickActionId.ManageRewards, "REWARD_MANAGER", true)]
    [InlineData(QuickActionId.ViewReports, "GAME_MANAGER", true)]
    [InlineData(QuickActionId.ViewReports, "REWARD_MANAGER", true)]
    public void QuickActions_CanAccessMatrix(QuickActionId id, string role, bool expected)
    {
        var visible = QuickActionsCatalog.ForRoles([role]);
        Assert.Equal(expected, visible.Any(a => a.Id == id));
    }

    [Fact]
    public void QuickActions_AllIconsAreLucideKnown()
    {
        var known = new HashSet<string>(["plus", "settings", "question", "live", "users", "gift", "chart"]);
        foreach (var action in QuickActionsCatalog.All)
        {
            Assert.Contains(action.Icon, known);
        }
    }

    [Fact]
    public void QuickActions_AllRoutesRequireNonEmptyLabelAndDescription()
    {
        foreach (var a in QuickActionsCatalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.Label));
            Assert.False(string.IsNullOrWhiteSpace(a.Description));
            Assert.False(string.IsNullOrWhiteSpace(a.Route));
            Assert.StartsWith("/admin/", a.Route);
        }
    }

    [Theory]
    [InlineData(MetricId.ActiveGames, "/admin/games?status=Active")]
    [InlineData(MetricId.ScheduledGames, "/admin/games?status=Scheduled")]
    [InlineData(MetricId.FinishedGames, "/admin/games?status=Finished")]
    [InlineData(MetricId.ConnectedPlayers, "/admin/players?view=online")]
    [InlineData(MetricId.ActivePlayers, "/admin/players?view=active")]
    [InlineData(MetricId.AvailableQuestions, "/admin/questions?status=Active")]
    [InlineData(MetricId.Categories, "/admin/categories?status=Active")]
    [InlineData(MetricId.Rewards, "/admin/rewards?status=Active")]
    [InlineData(MetricId.Redemptions, "/admin/rewards?status=Pending")]
    [InlineData(MetricId.GeneralStatistics, "/admin/reports?focus=general")]
    public void DrillDown_RouteMap_CoversAllTenMetrics(MetricId id, string expectedRoute)
    {
        Assert.Equal(expectedRoute, DashboardRouteMap.RouteFor(id));
        Assert.False(string.IsNullOrWhiteSpace(DashboardRouteMap.LabelFor(id)));
    }

    [Fact]
    public void DashboardSnapshot_TenMetricsInvariant()
    {
        // Contract: DashboardSnapshot must contain exactly 10 MetricValue entries (data-model.md)
        var metrics = Enum.GetValues<MetricId>();
        Assert.Equal(10, metrics.Length);
        foreach (var id in metrics)
        {
            var route = DashboardRouteMap.RouteFor(id);
            Assert.False(string.IsNullOrWhiteSpace(route));
        }
    }
}
