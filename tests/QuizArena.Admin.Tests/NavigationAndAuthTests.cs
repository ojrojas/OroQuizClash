using System.Security.Claims;
using QuizArena.Admin.Client;
using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Navigation;

namespace QuizArena.Admin.Tests;

/// <summary>
/// US1 (T034): role-filtered navigation (research R8) and AdminUserState claim mapping.
/// </summary>
public sealed class NavigationAndAuthTests
{
    private static AdminUserState User(params string[] roles) =>
        new(true, "Operator", roles, false);

    [Fact]
    public void VisibleSections_Admin_SeesAllTenSections()
    {
        var sections = AdminNavigation.VisibleSections(User(AdminUserState.AdminRole));

        Assert.Equal(10, sections.Count);
    }

    [Fact]
    public void VisibleSections_GameManager_ExcludesRewardsAndAudit()
    {
        var sections = AdminNavigation.VisibleSections(User(AdminUserState.GameManagerRole));

        Assert.Equal(8, sections.Count);
        Assert.DoesNotContain(sections, s => s.Href == "/admin/rewards");
        Assert.DoesNotContain(sections, s => s.Href == "/admin/audit");
    }

    [Fact]
    public void VisibleSections_RewardManager_SeesDashboardRewardsReports()
    {
        var sections = AdminNavigation.VisibleSections(User(AdminUserState.RewardManagerRole));

        Assert.Equal(3, sections.Count);
        Assert.Collection(sections.Select(s => s.Href).OrderBy(h => h),
            h => Assert.Equal("/admin/dashboard", h),
            h => Assert.Equal("/admin/reports", h),
            h => Assert.Equal("/admin/rewards", h));
    }

    [Fact]
    public void VisibleSections_NoRoles_SeesNothing()
    {
        Assert.Empty(AdminNavigation.VisibleSections(User()));
    }

    [Theory]
    [InlineData("/admin/audit", AdminUserState.AdminRole, true)]
    [InlineData("/admin/audit", AdminUserState.GameManagerRole, false)]
    [InlineData("/admin/rewards", AdminUserState.RewardManagerRole, true)]
    [InlineData("/admin/rewards", AdminUserState.GameManagerRole, false)]
    [InlineData("/admin/games", AdminUserState.GameManagerRole, true)]
    [InlineData("/admin/unknown", AdminUserState.AdminRole, false)]
    public void CanAccess_MatchesRoleMatrix(string href, string role, bool expected)
    {
        Assert.Equal(expected, AdminNavigation.CanAccess(User(role), href));
    }

    [Fact]
    public void FromPrincipal_MapsNameRolesAndMustChangePassword()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("name", "Ana Oro"),
            new Claim("roles", "ADMIN"),
            new Claim("roles", "GAME_MANAGER"),
            new Claim("must_change_password", "true")
        ], authenticationType: "TestAuth");

        var state = AdminUserState.FromPrincipal(new ClaimsPrincipal(identity));

        Assert.True(state.IsAuthenticated);
        Assert.Equal("Ana Oro", state.DisplayName);
        Assert.Equal(["ADMIN", "GAME_MANAGER"], state.Roles);
        Assert.True(state.MustChangePassword);
    }

    [Fact]
    public void FromPrincipal_DeduplicatesRoleClaimTypes()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("roles", "ADMIN"),
            new Claim("role", "ADMIN"),
            new Claim(ClaimTypes.Role, "ADMIN")
        ], authenticationType: "TestAuth");

        var state = AdminUserState.FromPrincipal(new ClaimsPrincipal(identity));

        Assert.Single(state.Roles);
    }

    [Fact]
    public void FromPrincipal_Unauthenticated_ReturnsAnonymous()
    {
        var state = AdminUserState.FromPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));

        Assert.False(state.IsAuthenticated);
        Assert.Empty(state.Roles);
        Assert.False(state.MustChangePassword);
    }

    [Fact]
    public void FromPrincipal_Null_ReturnsAnonymous()
    {
        Assert.Same(AdminUserState.Anonymous, AdminUserState.FromPrincipal(null));
    }

    [Fact]
    public void FromPrincipal_MustChangePasswordDefaultsFalse()
    {
        var identity = new ClaimsIdentity([new Claim("roles", "ADMIN")], authenticationType: "TestAuth");

        Assert.False(AdminUserState.FromPrincipal(new ClaimsPrincipal(identity)).MustChangePassword);
    }
}
