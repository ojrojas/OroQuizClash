using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Questions;

namespace OroQuizClash.Domain.Questions.Rules;

public sealed class PublishedQuestionMustHaveCorrectRule(QuestionStatus status, int correctCount) : IBusinessRule
{
    public bool IsBroken() => status == QuestionStatus.Published && correctCount != 1;
    public string Message => "A published question cannot be left without exactly 1 correct answer (QST-005).";
}
