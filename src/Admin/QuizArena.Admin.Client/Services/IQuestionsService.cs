using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface IQuestionsService
{
    Task<PagedResult<QuestionSummary>> GetQuestionsAsync(QuestionFilter filter, CancellationToken ct = default);
    Task<QuestionSummary> GetQuestionAsync(Guid id, CancellationToken ct = default);
    Task<QuestionSummary> CreateQuestionAsync(QuestionForm form, CancellationToken ct = default);
    Task<QuestionSummary> UpdateQuestionAsync(Guid id, QuestionForm form, CancellationToken ct = default);
    Task PublishQuestionAsync(Guid id, CancellationToken ct = default);
    Task ActivateQuestionAsync(Guid id, CancellationToken ct = default);
    Task DeactivateQuestionAsync(Guid id, CancellationToken ct = default);
    Task ArchiveQuestionAsync(Guid id, CancellationToken ct = default);
}
