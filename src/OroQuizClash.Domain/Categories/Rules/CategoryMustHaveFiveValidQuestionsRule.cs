using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Categories.Rules;

public sealed class CategoryMustHaveFiveValidQuestionsRule(int count) : IBusinessRule
{
    public bool IsBroken() => count < 5;
    public string Message => "Category cannot be published: requires at least 5 valid questions.";
}