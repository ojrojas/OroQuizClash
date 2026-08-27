using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class SufficientBalanceRule(int currentPoints, int amount) : IBusinessRule
{
    public bool IsBroken() => currentPoints < amount;
    public string Message => "Insufficient balance for this operation.";
}
