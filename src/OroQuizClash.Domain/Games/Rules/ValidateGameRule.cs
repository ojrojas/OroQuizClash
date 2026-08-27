using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class ValidateGameRule(GameStatus gameStatus) : IBusinessRule
{
    public bool IsBroken() => gameStatus != GameStatus.InProgress && gameStatus != GameStatus.RoundInProgress;
    public string Message => "Game is not in active state.";
}
