using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Questions.Rules;

public sealed class ExactlyOneCorrectAnswerRule(int correctCount) : IBusinessRule
{
    public bool IsBroken() => correctCount != 1;
    public string Message => "Question must have exactly 1 correct answer (QST-002).";
}
