namespace QuizArena.Admin.Client.Services;

public static class CategoryCatalogs
{
    public static readonly IReadOnlyList<string> ProgressionRules = ["Linear", "Progressive", "Adaptive", "CategorySpecific"];

    public static readonly IReadOnlyList<string> ExampleAreas = ["Matemáticas", "Historia", "Ciencia", "Tecnología", "Geografía", "Literatura", "Programación", "Finanzas"];

    public static readonly IReadOnlyList<int> Difficulties = [1, 2, 3, 4, 5];

    public static IReadOnlyList<(string Value, string Label)> ProgressionOptions =>
        ProgressionRules.Select(r => (r, r)).ToList();

    public static IReadOnlyList<(string Value, string Label)> AreaOptions =>
        ExampleAreas.Select(a => (a, a)).ToList();
}
