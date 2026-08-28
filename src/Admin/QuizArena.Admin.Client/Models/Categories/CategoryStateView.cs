namespace QuizArena.Admin.Client.Models.Categories;

public enum CategoryStateView
{
    Draft,
    Active,
    Inactive,
    Archived
}

public static class CategoryStateViewMap
{
    public static CategoryStateView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "ACTIVE" => CategoryStateView.Active,
        "INACTIVE" => CategoryStateView.Inactive,
        "ARCHIVED" => CategoryStateView.Archived,
        _ => CategoryStateView.Draft
    };

    public static string ToApi(CategoryStateView state) => state switch
    {
        CategoryStateView.Active => "ACTIVE",
        CategoryStateView.Inactive => "INACTIVE",
        CategoryStateView.Archived => "ARCHIVED",
        _ => "DRAFT"
    };

    public static string DisplayName(CategoryStateView state) => state switch
    {
        CategoryStateView.Draft => "Draft",
        CategoryStateView.Active => "Active",
        CategoryStateView.Inactive => "Inactive",
        CategoryStateView.Archived => "Archived",
        _ => state.ToString()
    };

    public static bool IsTerminal(CategoryStateView state) => state == CategoryStateView.Archived;
    public static bool CanEdit(CategoryStateView state) => state != CategoryStateView.Archived;
}
