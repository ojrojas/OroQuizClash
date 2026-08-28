using System.Net.Http.Json;
using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public class CategoriesServiceCore(HttpClient http, string prefix) : ICategoriesService
{
    private sealed record ApiCategoryResponse(
        Guid Id,
        string Name,
        string? Description,
        string KnowledgeArea,
        string AcademicLevel,
        int AgeMin,
        int AgeMax,
        int DifficultyLevel,
        IReadOnlyList<string> Tags,
        string Status,
        int ValidQuestionsCount,
        string RowVersion);

    private sealed record ApiPaginatedCategories(IReadOnlyList<ApiCategoryResponse> Items, int Total, int Page, int PageSize);

    private sealed record ApiCategoryRequest(
        string Name,
        string? Description,
        string KnowledgeArea,
        string AcademicLevel,
        int AgeMin,
        int AgeMax,
        int DifficultyLevel,
        List<string>? Tags,
        bool RequiresModeration = false);

    public async Task<PagedResult<CategorySummary>> GetCategoriesAsync(CategoryFilter filter, CancellationToken ct = default)
    {
        var query = QueryString.Build(new Dictionary<string, string?>
        {
            ["knowledgeArea"] = filter.KnowledgeArea,
            ["academicLevel"] = filter.AcademicLevel,
            ["difficultyLevel"] = filter.Difficulty?.ToString(),
            ["state"] = filter.Status is null ? null : CategoryStatusMap.ToApi(filter.Status.Value),
            ["tag"] = filter.Tag,
            ["page"] = filter.Page.ToString(),
            ["pageSize"] = filter.PageSize.ToString()
        });
        var result = await http.GetFromJsonAsync<ApiPaginatedCategories>($"{prefix}/categories{query}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return new PagedResult<CategorySummary>(
            result.Items.Select(Map).ToList(), result.Total, result.Page, result.PageSize);
    }

    public async Task<CategorySummary> GetCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var response = await http.GetFromJsonAsync<ApiCategoryResponse>($"{prefix}/categories/{id}", ct)
            ?? throw new ApiErrorException(ApiErrorView.Unknown);
        return Map(response);
    }

    public async Task<CategorySummary> CreateCategoryAsync(CategoryForm form, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync($"{prefix}/categories", ToRequest(form), ct);
        return Map(await response.ReadAsAsync<ApiCategoryResponse>(ct));
    }

    public async Task<CategorySummary> UpdateCategoryAsync(Guid id, CategoryForm form, CancellationToken ct = default)
    {
        var response = await http.PutAsJsonAsync($"{prefix}/categories/{id}", ToRequest(form), ct);
        return Map(await response.ReadAsAsync<ApiCategoryResponse>(ct));
    }

    public Task PublishCategoryAsync(Guid id, CancellationToken ct = default) => PostAsync($"{prefix}/categories/{id}/publish", ct);
    public Task ActivateCategoryAsync(Guid id, CancellationToken ct = default) => PostAsync($"{prefix}/categories/{id}/activate", ct);
    public Task DeactivateCategoryAsync(Guid id, CancellationToken ct = default) => PostAsync($"{prefix}/categories/{id}/deactivate", ct);
    public Task ArchiveCategoryAsync(Guid id, CancellationToken ct = default) => PostAsync($"{prefix}/categories/{id}/archive", ct);

    private async Task PostAsync(string url, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(new { }) };
        using var response = await http.SendAsync(request, ct);
        await response.ThrowIfApiErrorAsync(ct);
    }

    private static ApiCategoryRequest ToRequest(CategoryForm form) => new(
        form.Name.Trim(), form.Description, form.KnowledgeArea.Trim(), form.AcademicLevel.Trim(),
        form.AgeMin, form.AgeMax, form.Difficulty, form.Tags?.Select(t => t.Trim()).ToList());

    private static CategorySummary Map(ApiCategoryResponse c) => new(
        c.Id, c.Name, c.Description, c.KnowledgeArea, c.AcademicLevel,
        c.AgeMin, c.AgeMax, c.DifficultyLevel, c.Tags ?? [],
        CategoryStatusMap.FromApi(c.Status), c.ValidQuestionsCount);
}
