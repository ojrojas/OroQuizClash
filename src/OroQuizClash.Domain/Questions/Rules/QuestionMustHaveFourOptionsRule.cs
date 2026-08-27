using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Questions.Rules;

public sealed class QuestionMustHaveFourOptionsRule(int count) : IBusinessRule
{
    public bool IsBroken() => count != 4;
    public string Message => "Question must have exactly 4 answer options (QST-001).";
}
