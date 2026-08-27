using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class ValidateRoundRule(GameRound? currentRound) : IBusinessRule
{
    public bool IsBroken() => currentRound is null || currentRound.Status != GameStatus.RoundInProgress;
    public string Message => "Round is not in progress.";
}
