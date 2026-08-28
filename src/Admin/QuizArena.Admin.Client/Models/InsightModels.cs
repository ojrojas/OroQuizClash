using System.Security.Claims;

namespace QuizArena.Admin.Client.Models;

public sealed record ReportResult(
    string Title,
    DateRange? Period,
    IReadOnlyList<string> Columns,
    IReadOnlyList<object?[]> Rows);

public sealed record AuditEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    string ActorId,
    string ActorRoles,
    string Action,
    string Permission,
    string EntityType,
    string? EntityId,
    Guid? GameId,
    Guid? PlayerId,
    string CorrelationId,
    string Result,
    string? Reason,
    string? Summary,
    string? DetailJson);

public sealed record DashboardKpis(
    int ActiveGames,
    int PlayersOnline,
    int QuestionBankSize,
    int PendingRedemptions,
    decimal RewardsPaidPeriod,
    int GamesPeriod);

public enum LiveConnectionView
{
    Connected,
    Reconnecting,
    Disconnected
}

public sealed record LiveGameSummary(
    Guid GameId,
    string Name,
    Guid CategoryId,
    int PlayerCount,
    int RoundCount,
    GameStatusView Status,
    DateTimeOffset? StartedAt,
    LiveConnectionView ConnectionState = LiveConnectionView.Disconnected);

public sealed record AdminUserState(
    bool IsAuthenticated,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool MustChangePassword)
{
    public const string AdminRole = "ADMIN";
    public const string GameManagerRole = "GAME_MANAGER";
    public const string RewardManagerRole = "REWARD_MANAGER";

    public static AdminUserState Anonymous { get; } = new(false, string.Empty, [], false);

    public static AdminUserState FromPrincipal(ClaimsPrincipal? user)
    {
        if (user?.Identity is not { IsAuthenticated: true })
        {
            return Anonymous;
        }

        var roles = user.FindAll("roles").Select(c => c.Value)
            .Concat(user.FindAll("role").Select(c => c.Value))
            .Concat(user.FindAll(ClaimTypes.Role).Select(c => c.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var name = user.FindFirst("name")?.Value
            ?? user.FindFirst(ClaimTypes.Name)?.Value
            ?? user.Identity.Name
            ?? string.Empty;

        var mustChange = user.FindFirst("must_change_password")?.Value is "true" or "True";

        return new AdminUserState(true, name, roles, mustChange);
    }

    public bool HasRole(string role) => Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
}
