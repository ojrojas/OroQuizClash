using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Navigation;

/// <summary>
/// Role-filtered section catalog for the admin shell (research R8, spec US1).
/// ADMIN sees all 10 sections; GAME_MANAGER sees everything except Rewards and Audit;
/// REWARD_MANAGER sees Dashboard, Rewards and Reports. The UI hides sections the role
/// cannot access — the API remains the authoritative enforcer (403).
/// Pure logic (no Blazor dependency) so it is unit-testable (T034).
/// </summary>
public sealed class AdminNavigation
{
    public sealed record NavSection(string Title, string Href, string Icon, IReadOnlyList<string> Roles);

    public static readonly IReadOnlyList<NavSection> Sections =
    [
        new("Dashboard", "/admin/dashboard", "dashboard", [AdminUserState.AdminRole, AdminUserState.GameManagerRole, AdminUserState.RewardManagerRole]),
        new("Games", "/admin/games", "gamepad", [AdminUserState.AdminRole, AdminUserState.GameManagerRole]),
        new("Game Configuration", "/admin/games/configuration", "settings", [AdminUserState.AdminRole, AdminUserState.GameManagerRole]),
        new("Categories", "/admin/categories", "folder", [AdminUserState.AdminRole, AdminUserState.GameManagerRole]),
        new("Question Bank", "/admin/questions", "question", [AdminUserState.AdminRole, AdminUserState.GameManagerRole]),
        new("Players", "/admin/players", "users", [AdminUserState.AdminRole, AdminUserState.GameManagerRole]),
        new("Rewards", "/admin/rewards", "gift", [AdminUserState.AdminRole, AdminUserState.RewardManagerRole]),
        new("Live Games", "/admin/live", "live", [AdminUserState.AdminRole, AdminUserState.GameManagerRole]),
        new("Reports", "/admin/reports", "chart", [AdminUserState.AdminRole, AdminUserState.GameManagerRole, AdminUserState.RewardManagerRole]),
        new("Audit", "/admin/audit", "audit", [AdminUserState.AdminRole])
    ];

    public static IReadOnlyList<NavSection> VisibleSections(AdminUserState user) =>
        Sections.Where(s => s.Roles.Any(user.HasRole)).ToList();

    public static bool CanAccess(AdminUserState user, string href) =>
        Sections.FirstOrDefault(s => s.Href == href)?.Roles.Any(user.HasRole) ?? false;
}
