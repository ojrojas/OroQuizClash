namespace QuizArena.Admin.Client;

/// <summary>
/// Local authorization policy names mirroring QuizArena.Api SecurityPolicies
/// (contracts/oidc-config.md §6). Shared by both admin projects so pages can carry
/// [Authorize(Policy = ...)]. The API remains the authoritative enforcer.
/// </summary>
public static class AdminPolicies
{
    public const string AdminOnly = "AdminOnly";
    public const string AdminOrGameManager = "AdminOrGameManager";
    public const string RewardManagerOrAdmin = "RewardManagerOrAdmin";
    public const string AnyAdminRole = "AnyAdminRole";
}

/// <summary>
/// Role names as issued by OroIdentityServer (native JWT claim "roles").
/// </summary>
public static class AdminRoles
{
    public const string Admin = "ADMIN";
    public const string GameManager = "GAME_MANAGER";
    public const string RewardManager = "REWARD_MANAGER";
}
