using BuildingBlocks.Kernel.Domain.Repositories;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Infrastructure.Counters;

public sealed class EfQuestionCounter(IRepository<Question, QuestionId> repository) : Domain.Questions.Services.IQuestionCounter
{
    public async Task<int> CountValidAsync(CategoryId categoryId, CancellationToken cancellationToken = default)
    {
        var spec = new ValidQuestionSpecification(categoryId);
        return await repository.CountAsync(spec, cancellationToken);
    }
}
