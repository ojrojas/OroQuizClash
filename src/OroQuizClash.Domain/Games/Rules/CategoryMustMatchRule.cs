using BuildingBlocks.Kernel.Domain.Rules;

using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Domain.Games.Rules;

public sealed class CategoryMustMatchRule(CategoryId gameCategoryId, CategoryId questionCategoryId) : IBusinessRule
{
    public bool IsBroken() => gameCategoryId != questionCategoryId;
    public string Message => "Question category does not match the game's configured category.";
}
