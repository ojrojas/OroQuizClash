using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class ScoringStateValidRule(GameStatus status) : IBusinessRule
{
    public bool IsBroken() =>
        status != GameStatus.InProgress &&
        status != GameStatus.RoundInProgress &&
        status != GameStatus.RoundCompleted;

    public string Message => "Scoring operations are only valid while the game is active.";
}
