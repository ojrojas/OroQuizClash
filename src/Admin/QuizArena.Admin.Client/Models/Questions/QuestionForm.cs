namespace QuizArena.Admin.Client.Models.Questions;

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
