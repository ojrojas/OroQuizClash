using System.Text.RegularExpressions;

namespace QuizArena.Admin.Client.Models.Categories;

public sealed record CategoryForm(
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    string TargetAudience,
    IReadOnlyList<string> Tags,
    string? Color,
    string? Icon,
    ProgressionRule Progression)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        var name = Name?.Trim() ?? string.Empty;
        if (name.Length < 3 || name.Length > 100)
            errors[nameof(Name)] = ["Name must be 3-100 characters."];
        if (Description is not null && Description.Length > 500)
            errors[nameof(Description)] = ["Description must be at most 500 characters."];
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
            if (Tags.Count > 10) errors[nameof(Tags)] = ["At most 10 tags."];
            else if (Tags.Any(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length is < 2 or > 30))
                errors[nameof(Tags)] = ["Each tag must be 2-30 characters."];
            else if (Tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Tags.Count)
                errors[nameof(Tags)] = ["Tags must not contain duplicates."];
        }
        if (Color is not null && !Regex.IsMatch(Color, "^#[0-9A-Fa-f]{6}$"))
            errors[nameof(Color)] = ["Color must be hex #RRGGBB."];
        return errors;
    }

    public bool IsValid => Validate().Count == 0;
}
