namespace QuizArena.Admin.Client.Models;

public enum CategoryStatusView
{
    Draft,
    Active,
    Inactive,
    Archived
}

public static class CategoryStatusMap
{
    public static CategoryStatusView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "ACTIVE" => CategoryStatusView.Active,
        "INACTIVE" => CategoryStatusView.Inactive,
        "ARCHIVED" => CategoryStatusView.Archived,
        _ => CategoryStatusView.Draft
    };

    public static string ToApi(CategoryStatusView status) => status switch
    {
        CategoryStatusView.Active => "ACTIVE",
        CategoryStatusView.Inactive => "INACTIVE",
        CategoryStatusView.Archived => "ARCHIVED",
        _ => "DRAFT"
    };
}

public sealed record CategorySummary(
    Guid Id,
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    IReadOnlyList<string> Tags,
    CategoryStatusView Status,
    int ValidQuestionCount);

public sealed record CategoryForm(
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    IReadOnlyList<string> Tags,
    string TargetAudience = "General",
    string ProgressionRule = "Linear",
    string? Color = null,
    string? Icon = null)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        var name = Name?.Trim() ?? string.Empty;
        if (name.Length < 3 || name.Length > 100)
            errors[nameof(Name)] = ["Name must be 3-100 characters."];
        var area = KnowledgeArea?.Trim() ?? string.Empty;
        if (area.Length < 2 || area.Length > 100)
            errors[nameof(KnowledgeArea)] = ["Knowledge area must be 2-100 characters."];
        var level = AcademicLevel?.Trim() ?? string.Empty;
        if (level.Length < 2 || level.Length > 100)
            errors[nameof(AcademicLevel)] = ["Academic level must be 2-100 characters."];
        var target = TargetAudience?.Trim() ?? string.Empty;
        if (target.Length < 2 || target.Length > 100)
            errors[nameof(TargetAudience)] = ["Target audience must be 2-100 characters."];
        if (AgeMin is < 0 or > 120)
            errors[nameof(AgeMin)] = ["Minimum age must be 0-120."];
        if (AgeMax is < 0 or > 120 || AgeMax < AgeMin)
            errors[nameof(AgeMax)] = ["Maximum age must be 0-120 and at least the minimum age."];
        if (Difficulty is < 1 or > 5)
            errors[nameof(Difficulty)] = ["Difficulty must be 1-5."];
        if (Tags is not null)
        {
            if (Tags.Count > 10)
                errors[nameof(Tags)] = ["At most 10 tags."];
            else if (Tags.Any(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length is < 2 or > 30))
                errors[nameof(Tags)] = ["Each tag must be 2-30 characters."];
            else if (Tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Tags.Count)
                errors[nameof(Tags)] = ["Tags must not contain duplicates."];
        }
        if (Color is not null && !System.Text.RegularExpressions.Regex.IsMatch(Color, "^#[0-9A-Fa-f]{6}$"))
            errors[nameof(Color)] = ["Color must be hex #RRGGBB."];
        var allowedProgression = new[] { "Linear", "Progressive", "Adaptive", "CategorySpecific" };
        if (!allowedProgression.Contains(ProgressionRule))
            errors[nameof(ProgressionRule)] = ["Progression rule must be Linear, Progressive, Adaptive or CategorySpecific."];
        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}

public enum QuestionStatusView
{
    Draft,
    Active,
    Inactive,
    Archived
}

public static class QuestionStatusMap
{
    public static QuestionStatusView FromApi(string? apiStatus) => apiStatus?.ToUpperInvariant() switch
    {
        "ACTIVE" or "PUBLISHED" => QuestionStatusView.Active,
        "INACTIVE" => QuestionStatusView.Inactive,
        "ARCHIVED" => QuestionStatusView.Archived,
        _ => QuestionStatusView.Draft
    };

    public static string ToApi(QuestionStatusView status) => status switch
    {
        QuestionStatusView.Active => "ACTIVE",
        QuestionStatusView.Inactive => "INACTIVE",
        QuestionStatusView.Archived => "ARCHIVED",
        _ => "DRAFT"
    };
}

public sealed record QuestionSummary(
    Guid Id,
    string Text,
    Guid CategoryId,
    int Difficulty,
    QuestionStatusView Status,
    bool InUseByLiveGame,
    DateTimeOffset CreatedAt);

public sealed record OptionForm(string Text, bool IsCorrect);

public sealed record QuestionForm(
    string Text,
    Guid CategoryId,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    IReadOnlyList<OptionForm> Options,
    string? Explanation = null,
    int TimePerQuestion = 30)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(Text) || Text.Trim().Length < 10)
            errors[nameof(Text)] = ["Question text must be at least 10 characters."];
        if (CategoryId == Guid.Empty)
            errors[nameof(CategoryId)] = ["Category is required."];
        if (Difficulty is < 1 or > 5)
            errors[nameof(Difficulty)] = ["Difficulty must be 1-5."];
        var level = AcademicLevel?.Trim() ?? string.Empty;
        if (level.Length < 2 || level.Length > 100)
            errors[nameof(AcademicLevel)] = ["Academic level must be 2-100 characters."];
        if (AgeMin is < 0 or > 120)
            errors[nameof(AgeMin)] = ["Minimum age must be 0-120."];
        if (AgeMax is < 0 or > 120 || AgeMax < AgeMin)
            errors[nameof(AgeMax)] = ["Maximum age must be 0-120 and at least the minimum age."];
        if (TimePerQuestion is < 5 or > 300)
            errors[nameof(TimePerQuestion)] = ["Time per question must be 5-300 seconds."];
        if (Explanation is not null && Explanation.Length > 1000)
            errors[nameof(Explanation)] = ["Explanation must be at most 1000 characters."];
        if (Options is null || Options.Count != 4)
            errors[nameof(Options)] = ["Exactly 4 answer options are required."];
        else
        {
            if (Options.Count(o => o.IsCorrect) != 1)
                errors[nameof(Options)] = ["Exactly 1 option must be marked correct."];
            else if (Options.Any(o => string.IsNullOrWhiteSpace(o.Text) || o.Text.Trim().Length is < 1 or > 200))
                errors[nameof(Options)] = ["Each option text must be 1-200 characters."];
        }
        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}
