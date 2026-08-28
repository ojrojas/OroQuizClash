namespace QuizArena.Admin.Client.Models.Questions;

public enum QuestionStateView
{
    Draft,
    Active,
    Inactive,
    Archived
}

public static class QuestionStateViewMap
{
    public static QuestionStateView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "ACTIVE" or "PUBLISHED" => QuestionStateView.Active,
        "INACTIVE" => QuestionStateView.Inactive,
        "ARCHIVED" => QuestionStateView.Archived,
        _ => QuestionStateView.Draft
    };

    public static string ToApi(QuestionStateView state) => state switch
    {
        QuestionStateView.Active => "ACTIVE",
        QuestionStateView.Inactive => "INACTIVE",
        QuestionStateView.Archived => "ARCHIVED",
        _ => "DRAFT"
    };

    public static string DisplayName(QuestionStateView state) => state switch
    {
        QuestionStateView.Draft => "Draft",
        QuestionStateView.Active => "Active",
        QuestionStateView.Inactive => "Inactive",
        QuestionStateView.Archived => "Archived",
        _ => state.ToString()
    };
}
