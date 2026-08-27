using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Questions.Rules;

public sealed class QuestionCanUpdateRule(QuestionStatus status) : IBusinessRule
{
    public bool IsBroken() => status == QuestionStatus.Archived;
    public string Message => "Cannot update a question in ARCHIVED state.";
}
