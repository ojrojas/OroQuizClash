using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface ICategoriesService
{
    Task<PagedResult<CategorySummary>> GetCategoriesAsync(CategoryFilter filter, CancellationToken ct = default);
    Task<CategorySummary> GetCategoryAsync(Guid id, CancellationToken ct = default);
    Task<CategorySummary> CreateCategoryAsync(CategoryForm form, CancellationToken ct = default);
    Task<CategorySummary> UpdateCategoryAsync(Guid id, CategoryForm form, CancellationToken ct = default);
    Task PublishCategoryAsync(Guid id, CancellationToken ct = default);
    Task ActivateCategoryAsync(Guid id, CancellationToken ct = default);
    Task DeactivateCategoryAsync(Guid id, CancellationToken ct = default);
    Task ArchiveCategoryAsync(Guid id, CancellationToken ct = default);
}
