namespace QuizArena.Admin.Client.Models.GameConfiguration;

public sealed record GameConfiguration(
    Guid GameId,
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
    DateTimeOffset? ScheduledAt,
    GameStateView Status,
    string RowVersion);

public sealed record GameSummary(
    Guid Id,
    string Name,
    Guid CategoryId,
    string CategoryName,
    GameStateView Status,
    int NumberOfRounds,
    int MaxPlayers,
    int TimePerQuestion,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt,
    string RowVersion);

public sealed record GameDetail(
    Guid Id,
    string Name,
    string? Description,
    Guid CategoryId,
    string CategoryName,
    GameStateView Status,
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
    DateTimeOffset? ScheduledAt,
    string RowVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyList<GameStateTransition> History);

public sealed record GameStateTransition(
    GameStateView From,
    GameStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);

public sealed record GameAuditEntry(
    Guid GameId,
    string ActorId,
    DateTimeOffset Timestamp,
    GameStateView FromState,
    GameStateView ToState,
    IReadOnlyDictionary<string, string> ChangedFields,
    string CorrelationId,
    string Result);

public sealed record CreateGameRequest(
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
    DateTimeOffset? ScheduledAt);

public sealed record UpdateGameRequest(
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
    DateTimeOffset? ScheduledAt,
    string RowVersion);

public sealed record GameFilter(
    GameStateView? Status = null,
    Guid? CategoryId = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
