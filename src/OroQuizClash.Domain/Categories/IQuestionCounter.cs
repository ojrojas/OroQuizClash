namespace OroQuizClash.Domain.Categories;

public interface IQuestionCounter
{
    Task<int> CountValidAsync(CategoryId categoryId, CancellationToken cancellationToken = default);
}