using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class TimeLimitRangeRule(int seconds) : IBusinessRule
{
    public bool IsBroken() => seconds < 5 || seconds > 300;
    public string Message => "TimeLimitPerQuestion must be between 5 and 300 seconds.";
}