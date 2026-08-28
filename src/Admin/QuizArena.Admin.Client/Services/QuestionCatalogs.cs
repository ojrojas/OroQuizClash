namespace QuizArena.Admin.Client.Services;

public static class QuestionCatalogs
{
    public static readonly IReadOnlyList<int> Difficulties = [1, 2, 3, 4, 5];
    public static readonly IReadOnlyList<string> AcademicLevels = ["Primaria", "Secundaria", "Preparatoria", "Universitario", "Posgrado"];
    public static readonly IReadOnlyList<(int Min, int Max)> AgeRanges = [(5, 8), (9, 12), (13, 17), (18, 25), (26, 60)];
    public static readonly IReadOnlyList<int> TimeOptions = [5, 10, 15, 30, 45, 60, 90, 120, 300];

    public static IReadOnlyList<(string Value, string Label)> DifficultyOptions =>
        Difficulties.Select(d => (d.ToString(), $"Dificultad {d}")).ToList();

    public static IReadOnlyList<(string Value, string Label)> TimeOptionsWithLabel =>
        TimeOptions.Select(t => (t.ToString(), $"{t}s")).ToList();
}
