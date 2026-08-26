using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Infrastructure.Categories;

public sealed class CategoryValidatorStub : ICategoryValidator
{
    private static readonly Guid NotPublishedId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    public Task<bool> ExistsAsync(CategoryId categoryId, CancellationToken cancellationToken = default)
    {
        if (categoryId.Value == Guid.Empty) return Task.FromResult(false);
        return Task.FromResult(true);
    }

    public Task<bool> IsPublishedAsync(CategoryId categoryId, CancellationToken cancellationToken = default)
    {
        if (categoryId.Value == Guid.Empty) return Task.FromResult(false);
        if (categoryId.Value == NotPublishedId) return Task.FromResult(false);
        return Task.FromResult(true);
    }
}