using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Questions.Rules;

public sealed class QuestionStateTransitionRule(QuestionStatus from, QuestionStatus to) : IBusinessRule
{
    public bool IsBroken() => !QuestionStatus.IsValidTransition(from, to);
    public string Message => $"Invalid transition from {from.Name} to {to.Name}.";
}
