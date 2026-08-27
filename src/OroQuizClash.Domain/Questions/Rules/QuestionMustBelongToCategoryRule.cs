using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Questions.Rules;

public sealed class QuestionMustBelongToCategoryRule(Guid? categoryId, bool exists) : IBusinessRule
{
    public bool IsBroken() => categoryId is null || categoryId == Guid.Empty || !exists;
    public string Message => "Question must belong to an existing category (QST-003).";
}
