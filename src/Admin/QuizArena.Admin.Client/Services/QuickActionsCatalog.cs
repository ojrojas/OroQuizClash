namespace QuizArena.Admin.Client.Services;

public enum QuickActionId
{
    CreateGame,
    ConfigureGame,
    ManageQuestions,
    ViewActiveGames,
    ViewPlayers,
    ManageRewards,
    ViewReports
}

public enum QuickActionRole
{
    Admin,
    GameManager,
    RewardManager
}

public sealed record QuickAction(
    QuickActionId Id,
    string Label,
    string Description,
    string Icon,
    string Route,
    IReadOnlyList<QuickActionRole> AllowedRoles);

public static class QuickActionsCatalog
{
    public static readonly IReadOnlyList<QuickAction> All =
    [
        new(QuickActionId.CreateGame, "Crear juego", "Crear un nuevo juego", "plus", "/admin/games/new", [QuickActionRole.Admin, QuickActionRole.GameManager]),
        new(QuickActionId.ConfigureGame, "Configurar juego", "Configurar juego existente", "settings", "/admin/games/configuration", [QuickActionRole.Admin, QuickActionRole.GameManager]),
        new(QuickActionId.ManageQuestions, "Gestionar preguntas", "Banco de preguntas", "question", "/admin/questions", [QuickActionRole.Admin, QuickActionRole.GameManager]),
        new(QuickActionId.ViewActiveGames, "Ver juegos activos", "Juegos en curso", "live", "/admin/games?status=Active", [QuickActionRole.Admin, QuickActionRole.GameManager]),
        new(QuickActionId.ViewPlayers, "Ver jugadores", "Listado de jugadores", "users", "/admin/players", [QuickActionRole.Admin, QuickActionRole.GameManager]),
        new(QuickActionId.ManageRewards, "Gestionar premios", "Catálogo y canjes", "gift", "/admin/rewards", [QuickActionRole.Admin, QuickActionRole.RewardManager]),
        new(QuickActionId.ViewReports, "Consultar reportes", "Reportes y estadísticas", "chart", "/admin/reports", [QuickActionRole.Admin, QuickActionRole.GameManager, QuickActionRole.RewardManager])
    ];

    public static IReadOnlyList<QuickAction> ForRoles(IReadOnlyList<string> roles)
    {
        var mapped = roles.Select(MapRole).Where(r => r is not null).Cast<QuickActionRole>().Distinct().ToList();
        if (mapped.Count == 0) return [];
        return All.Where(a => a.AllowedRoles.Any(mapped.Contains)).ToList();
    }

    private static QuickActionRole? MapRole(string role) => role.ToUpperInvariant() switch
    {
        "ADMIN" => QuickActionRole.Admin,
        "GAME_MANAGER" => QuickActionRole.GameManager,
        "REWARD_MANAGER" => QuickActionRole.RewardManager,
        _ => null
    };
}
