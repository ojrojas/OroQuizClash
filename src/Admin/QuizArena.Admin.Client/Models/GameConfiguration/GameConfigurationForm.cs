namespace QuizArena.Admin.Client.Models.GameConfiguration;

public sealed record GameConfigurationForm(
    string Name,
    string? Description,
    Guid CategoryId,
    int NumberOfRounds,
    int MaxPlayers,
    int TimePerQuestion,
    int InitialDifficulty,
    DifficultyStrategy DifficultyProgression,
    ScoringSystem Scoring,
    int PointsPerRound,
    SecuredPointsPolicy SecuredPoints,
    WithdrawalPolicy WithdrawalPolicy,
    LossPolicy FinishPolicy,
    Guid? FinalRewardId,
    Guid? ConsolationRewardId,
    DateTimeOffset? ScheduledAt)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        var name = Name?.Trim() ?? string.Empty;
        if (name.Length < 3 || name.Length > 100)
            errors[nameof(Name)] = ["Name must be 3-100 characters."];
        if (Description is not null && Description.Length > 500)
            errors[nameof(Description)] = ["Description must be at most 500 characters."];
        if (CategoryId == Guid.Empty)
            errors[nameof(CategoryId)] = ["Category is required."];
        if (NumberOfRounds is < 5 or > 10)
            errors[nameof(NumberOfRounds)] = ["Number of rounds must be 5-10."];
        if (MaxPlayers is < 2 or > 1000)
            errors[nameof(MaxPlayers)] = ["Max players must be 2-1000."];
        if (TimePerQuestion is < 5 or > 300)
            errors[nameof(TimePerQuestion)] = ["Time per question must be 5-300 seconds."];
        if (InitialDifficulty is < 1 or > 5)
            errors[nameof(InitialDifficulty)] = ["Initial difficulty must be 1-5."];
        if (PointsPerRound < 0)
            errors[nameof(PointsPerRound)] = ["Points per round must be non-negative."];
        if (ScheduledAt is not null)
        {
            if (ScheduledAt.Value.ToUniversalTime() < DateTimeOffset.UtcNow.AddMinutes(5))
                errors[nameof(ScheduledAt)] = ["Scheduled time must be at least 5 minutes in the future."];
        }
        if (FinalRewardId is not null && FinalRewardId == Guid.Empty)
            errors[nameof(FinalRewardId)] = ["Invalid final reward."];
        if (ConsolationRewardId is not null && ConsolationRewardId == Guid.Empty)
            errors[nameof(ConsolationRewardId)] = ["Invalid consolation reward."];
        if (FinalRewardId is not null && ConsolationRewardId is not null && FinalRewardId == ConsolationRewardId)
            errors[nameof(ConsolationRewardId)] = ["Consolation reward must differ from final reward when required."];
        if (SecuredPoints == SecuredPointsPolicy.KeepCheckpoint && NumberOfRounds < 5)
            errors[nameof(SecuredPoints)] = ["Secured points require at least 5 rounds."];
        return errors;
    }

    public bool IsValid => Validate().Count == 0;

    public static GameConfigurationForm FromRequest(CreateGameRequest req) => new(
        req.Name, req.Description, req.CategoryId, req.NumberOfRounds, req.MaxPlayers,
        req.TimePerQuestion, req.InitialDifficulty, req.DifficultyProgression, req.Scoring,
        req.PointsPerRound, req.SecuredPoints, req.WithdrawalPolicy, req.FinishPolicy,
        req.FinalRewardId, req.ConsolationRewardId, req.ScheduledAt);
}
