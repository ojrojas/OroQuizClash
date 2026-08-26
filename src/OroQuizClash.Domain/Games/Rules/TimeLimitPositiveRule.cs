using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class TimeLimitPositiveRule(int seconds) : IBusinessRule
{
    public bool IsBroken() => seconds <= 0;
    public string Message => "TimeLimitPerQuestion must be positive.";
}