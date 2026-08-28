namespace QuizArena.Admin.Client.Models.Categories;

public enum ProgressionRule
{
    Linear,
    Progressive,
    Adaptive,
    CategorySpecific
}

public sealed record CategoryMetadata(
    IReadOnlyList<string> Tags,
    string? Color,
    string? Icon)
{
    public IReadOnlyDictionary<string, string[]> Validate()
    {
        var errors = new Dictionary<string, string[]>();
        if (Tags is not null)
        {
            if (Tags.Count > 10) errors[nameof(Tags)] = ["At most 10 tags."];
            else if (Tags.Any(t => string.IsNullOrWhiteSpace(t) || t.Trim().Length is < 2 or > 30))
                errors[nameof(Tags)] = ["Each tag must be 2-30 characters."];
            else if (Tags.Distinct(StringComparer.OrdinalIgnoreCase).Count() != Tags.Count)
                errors[nameof(Tags)] = ["Tags must not contain duplicates."];
        }
        if (Color is not null && !System.Text.RegularExpressions.Regex.IsMatch(Color, "^#[0-9A-Fa-f]{6}$"))
            errors[nameof(Color)] = ["Color must be hex #RRGGBB."];
        return errors;
    }
}
