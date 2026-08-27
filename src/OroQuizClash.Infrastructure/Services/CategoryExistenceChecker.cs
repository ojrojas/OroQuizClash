using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions.Services;

using BuildingBlocks.Kernel.Domain.Repositories;

namespace OroQuizClash.Infrastructure.Services;

public sealed class CategoryExistenceChecker(IRepository<Category, CategoryId> repository) : ICategoryExistenceChecker
{
    public async Task<bool> ExistsAsync(CategoryId categoryId, CancellationToken cancellationToken = default)
    {
        var category = await repository.GetByIdAsync(categoryId, cancellationToken);
        if (category is null) return false;
        // Archived categories are not valid for new questions (QST-003)
        return !category.Status.IsTerminal;
    }
}
