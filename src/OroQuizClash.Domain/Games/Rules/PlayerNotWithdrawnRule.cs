using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class PlayerNotWithdrawnRule(bool isWithdrawn) : IBusinessRule
{
    public bool IsBroken() => isWithdrawn;
    public string Message => "Player has withdrawn and cannot receive scoring operations.";
}
