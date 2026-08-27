using BuildingBlocks.Kernel.Domain.Results;

namespace OroQuizClash.Domain.Shared.Errors;

public static class GameErrors
{
    public static Error InvalidName => Error.Validation("InvalidGameConfiguration.InvalidName", "Game name must be 3-100 characters and not empty.");
    public static Error MinRoundsTooLow => Error.Validation("InvalidGameConfiguration.MinRoundsTooLow", "MinRounds must be >= 5.");
    public static Error InvalidRange => Error.Validation("InvalidGameConfiguration.InvalidRange", "Range invalid: min must be <= max and min >= 1.");
    public static Error InvalidTimeLimit => Error.Validation("InvalidGameConfiguration.InvalidTimeLimit", "TimeLimitPerQuestion must be between 5 and 300 seconds.");
    public static Error DifficultyStrategyRequired => Error.Validation("InvalidGameConfiguration.DifficultyStrategyRequired", "Difficulty strategy is required.");
    public static Error IncompatibleDifficulty => Error.Validation("InvalidGameConfiguration.IncompatibleDifficulty", "Initial difficulty is incompatible with strategy.");
    public static Error PoliciesRequired => Error.Validation("InvalidGameConfiguration.PoliciesRequired", "Loss and withdrawal policies are required.");
    public static Error InvalidGameConfiguration(string detail) => Error.Validation("InvalidGameConfiguration", detail);
    public static Error CategoryNotFound => Error.NotFound("CategoryNotFound", "Category does not exist.");
    public static Error CategoryNotReady => Error.Validation("CategoryNotReady", "Category is not published or has fewer than 5 valid questions.");
    public static Error InvalidGameState => Error.Validation("InvalidGameState", "Invalid game state for this operation.");
    public static Error InvalidGameStateDetail(string detail) => Error.Validation("InvalidGameState", detail);
    public static Error ConfigurationImmutable => Error.Validation("InvalidGameState.ConfigurationImmutable", "Configuration cannot be modified after game has started.");
    public static Error GameNotFound => Error.NotFound("GameNotFound", "Game not found.");
    public static Error NotEnoughPlayers => Error.Validation("NotEnoughPlayers", "Not enough players to start the game.");
    public static Error PlayerAlreadyJoined => Error.Conflict("PlayerAlreadyJoined", "Player already joined this game.");
    public static Error GameFull => Error.Conflict("GameFull", "Game has reached maximum players.");
    public static Error RoundAlreadyInProgress => Error.Validation("RoundAlreadyInProgress", "A round is already in progress.");
    public static Error PreviousRoundNotCompleted => Error.Validation("PreviousRoundNotCompleted", "Previous round has not completed.");
    public static Error NoActiveRound => Error.Validation("NoActiveRound", "No active round to submit answer.");
    public static Error ConcurrencyConflict => Error.Conflict("ConcurrencyConflict", "Game was modified by another request. Please reload and retry.");
    public static Error NoAvailableQuestion => Error.Validation("NoAvailableQuestion", "No available question for next round.");
    public static Error InvalidReason => Error.Validation("InvalidReason", "Reason must be 3-500 characters.");

    public static Error PlayerNotInGame => Error.Validation("PlayerNotInGame", "Player is not in progress in this game.");
    public static Error GameNotActive => Error.Validation("GameNotActive", "Game is not in active state.");
    public static Error QuestionNotActive => Error.Validation("QuestionNotActive", "Round is not in progress.");
    public static Error InvalidAnswer => Error.Validation("InvalidAnswer", "Answer option does not belong to the active question.");
    public static Error AnswerTimeout => Error.Validation("AnswerTimeout", "Answer submitted after time limit.");
    public static Error AnswerImmutable => Error.Validation("AnswerImmutable", "Answer cannot be modified after evaluation.");

    public static Error InsufficientPoints => Error.Validation("InsufficientPoints", "Player does not have enough points for this operation.");
    public static Error InvalidScoringState => Error.Validation("InvalidScoringState", "Game is not in a valid state for scoring operations.");
    public static Error AdjustmentReasonRequired => Error.Validation("AdjustmentReasonRequired", "Adjustment reason must be 3-500 characters.");
    public static Error PlayerAlreadyWithdrawn => Error.Conflict("PlayerAlreadyWithdrawn", "Player has already withdrawn from this game.");
    public static Error InvalidAdjustmentAmount => Error.Validation("InvalidAdjustmentAmount", "Adjustment amount must not be zero.");
    public static Error PlayerAlreadyEliminated => Error.Conflict("PlayerAlreadyEliminated", "Player has been eliminated and cannot withdraw.");
    public static Error ParticipationAlreadyFinished => Error.Conflict("ParticipationAlreadyFinished", "Player participation has already finished.");
}