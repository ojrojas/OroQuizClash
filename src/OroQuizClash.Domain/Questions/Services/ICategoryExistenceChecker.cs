using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Domain.Questions.Services;

public interface ICategoryExistenceChecker
{
    Task<bool> ExistsAsync(CategoryId categoryId, CancellationToken cancellationToken = default);
}
