using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public class QuestionsServiceCore(HttpClient http, string prefix) : IQuestionsService
{
    private sealed record ApiOptionResponse(Guid Id, string Text, bool IsCorrect, int DisplayOrder);

    private sealed record ApiQuestionResponse(
        Guid Id,
        string Text,
        Guid CategoryId,
        int Difficulty,
        string AcademicLevel,
        int AgeMin,
        int AgeMax,
        string Status,
        IReadOnlyList<ApiOptionResponse> AnswerOptions,
        string RowVersion,
        DateTimeOffset CreatedAt);

    private sealed record ApiPaginatedQuestions(IReadOnlyList<ApiQuestionResponse> Items, int Total, int Page, int PageSize);

    private sealed record ApiOptionInput(string Text, bool IsCorrect, int DisplayOrder);

    private sealed record ApiQuestionRequest(
        string Text,
        Guid CategoryId,
        int Difficulty,
        string AcademicLevel,
        int AgeMin,
        int AgeMax,
        List<ApiOptionInput> AnswerOptions);

    public async Task<PagedResult<QuestionSummary>> GetQuestionsAsync(QuestionFilter filter, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["categoryId"] = filter.CategoryId?.ToString(),
            ["difficulty"] = filter.Difficulty?.ToString(),
            ["status"] = filter.Status is null ? null : QuestionStatusMap.ToApi(filter.Status.Value),
            ["search"] = filter.Search,
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var result = await http.GetFromJsonAsync<ApiPaginatedQuestions>($"{prefix}/questions{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new PagedResult<QuestionSummary>(
            result.Items.Select(Map).ToList(), result.Total, result.Page, result.PageSize);
    }

    public async Task<QuestionSummary> GetQuestionAsync(Guid id, CancellationToken ct = default) =>
        Map(await http.GetFromJsonAsync<ApiQuestionResponse>($"{prefix}/questions/{id}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown));

    public async Task<QuestionSummary> CreateQuestionAsync(QuestionForm form, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"{prefix}/questions", ToRequest(form), ct);
        return Map(await response.ReadAsAsync<ApiQuestionResponse>(ct));
    }

    public async Task<QuestionSummary> UpdateQuestionAsync(Guid id, QuestionForm form, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"{prefix}/questions/{id}", ToRequest(form), ct);
        return Map(await response.ReadAsAsync<ApiQuestionResponse>(ct));
    }

    public Task PublishQuestionAsync(Guid id, CancellationToken ct = default) => PostAsync($"{prefix}/questions/{id}/publish", ct);
    public Task ActivateQuestionAsync(Guid id, CancellationToken ct = default) => PostAsync($"{prefix}/questions/{id}/activate", ct);
    public Task DeactivateQuestionAsync(Guid id, CancellationToken ct = default) => PostAsync($"{prefix}/questions/{id}/deactivate", ct);
    public Task ArchiveQuestionAsync(Guid id, CancellationToken ct = default) => PostAsync($"{prefix}/questions/{id}/archive", ct);

    private async Task PostAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { }) };
        using var response = await http.SendAsync(request, ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    private static ApiQuestionRequest ToRequest(QuestionForm form) => new(
        form.Text.Trim(), form.CategoryId, form.Difficulty, form.AcademicLevel, form.AgeMin, form.AgeMax,
        form.Options.Select((o, i) => new ApiOptionInput(o.Text.Trim(), o.IsCorrect, i)).ToList());

    private static QuestionSummary Map(ApiQuestionResponse q) => new(
        q.Id, q.Text, q.CategoryId, q.Difficulty,
        QuestionStatusMap.FromApi(q.Status), InUseByLiveGame: false, q.CreatedAt);
}
