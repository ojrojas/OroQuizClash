namespace QuizArena.Admin.Client.Models.Players;

public sealed record PlayerScoreSummary(
    int TotalPoints,
    int SecuredPoints,
    int AvailablePoints);

public sealed record PlayerDetail(
    Guid PlayerId,
    string DisplayName,
    string Email,
    string? TenantId,
    string? IdentificationType,
    string? IdentificationValue,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastActiveAt,
    PlayerStateView State,
    PlayerScoreSummary ScoreSummary,
    int TotalParticipations,
    string RowVersion);

public sealed record GameHistoryEntry(
    Guid GameId,
    string GameName,
    Guid CategoryId,
    string CategoryName,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int RoundCount,
    int? PlayerScore,
    int? PlayerRank);

public sealed record GameHistoryFilter(
    string? Search = null,
    string? Status = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (From.HasValue && To.HasValue && From.Value > To.Value)
            errors["DateRange"] = ["Desde debe ser ≤ Hasta."];
        if (Page < 1) errors[nameof(Page)] = ["Page debe ser ≥1."];
        if (PageSize is < 1 or > 100) errors[nameof(PageSize)] = ["PageSize debe estar entre 1 y 100."];
        return errors;
    }
}

public sealed record PlayerParticipation(
    Guid ParticipationId,
    Guid GameId,
    string GameName,
    DateTimeOffset JoinedAt,
    string State,
    string GameStatus,
    string? Role);

public sealed record ParticipationFilter(
    string? State = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Page = 1,
    int PageSize = 20)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (From.HasValue && To.HasValue && From.Value > To.Value)
            errors["DateRange"] = ["Desde debe ser ≤ Hasta."];
        if (Page < 1) errors[nameof(Page)] = ["Page debe ser ≥1."];
        return errors;
    }
}

public sealed record PlayerResult(
    Guid PlayerId,
    Guid GameId,
    int TotalScore,
    int SecuredScore,
    int Rank,
    int CorrectAnswers,
    int TotalAnswers,
    TimeSpan Duration,
    IReadOnlyList<PointTransactionView> Bonuses,
    IReadOnlyList<PointTransactionView> Penalties);
