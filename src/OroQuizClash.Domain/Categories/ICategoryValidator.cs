namespace OroQuizClash.Domain.Categories;

public interface ICategoryValidator
{
    Task<bool> ExistsAsync(CategoryId categoryId, CancellationToken cancellationToken = default);
    Task<bool> IsPublishedAsync(CategoryId categoryId, CancellationToken cancellationToken = default);
}