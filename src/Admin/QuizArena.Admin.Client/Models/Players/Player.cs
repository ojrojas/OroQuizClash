namespace QuizArena.Admin.Client.Models.Players;

public enum PlayerStateView
{
    Active = 0,
    InGame = 1,
    Withdrawn = 2,
    Inactive = 3
}

public static class PlayerStateMap
{
    public static PlayerStateView FromApi(string? api) => api?.ToUpperInvariant() switch
    {
        "INGAME" or "IN_GAME" => PlayerStateView.InGame,
        "WITHDRAWN" => PlayerStateView.Withdrawn,
        "INACTIVE" => PlayerStateView.Inactive,
        _ => PlayerStateView.Active
    };

    public static string ToApi(PlayerStateView state) => state switch
    {
        PlayerStateView.InGame => "InGame",
        PlayerStateView.Withdrawn => "Withdrawn",
        PlayerStateView.Inactive => "Inactive",
        _ => "Active"
    };

    public static string DisplayName(PlayerStateView state) => state switch
    {
        PlayerStateView.Active => "Activo",
        PlayerStateView.InGame => "En partida",
        PlayerStateView.Withdrawn => "Retirado",
        PlayerStateView.Inactive => "Inactivo",
        _ => state.ToString()
    };
}

public sealed record PlayerSummary(
    Guid PlayerId,
    string DisplayName,
    string Email,
    string? TenantId,
    string? IdentificationType,
    string? IdentificationValue,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastActiveAt,
    PlayerStateView State);

public sealed record PlayerFilter(
    string? Search = null,
    int Page = 1,
    int PageSize = 20)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Search is not null && Search.Length > 100)
            errors[nameof(Search)] = ["La búsqueda debe tener como máximo 100 caracteres."];
        if (Page < 1)
            errors[nameof(Page)] = ["Page debe ser ≥1."];
        if (PageSize is < 1 or > 100)
            errors[nameof(PageSize)] = ["PageSize debe estar entre 1 y 100."];
        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}
