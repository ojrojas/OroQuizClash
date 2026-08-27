using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class ValidateTimeRule(TimeSpan elapsed, int timeLimit) : IBusinessRule
{
    public bool IsBroken() => elapsed.TotalSeconds > timeLimit;
    public string Message => "Answer submitted after time limit.";
}
