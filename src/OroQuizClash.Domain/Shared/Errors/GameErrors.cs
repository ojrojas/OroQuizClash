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
    public static Error ConfigurationImmutable => Error.Validation("InvalidGameState.ConfigurationImmutable", "Configuration cannot be modified after game has started.");
    public static Error GameNotFound => Error.NotFound("GameNotFound", "Game not found.");
}