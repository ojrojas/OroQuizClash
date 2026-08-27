using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class ValidatePlayerRule(bool isPlayerInProgress) : IBusinessRule
{
    public bool IsBroken() => !isPlayerInProgress;
    public string Message => "Player is not in progress in this game.";
}
