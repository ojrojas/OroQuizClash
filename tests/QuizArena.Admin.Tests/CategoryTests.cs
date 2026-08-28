using QuizArena.Admin.Client.Models;
using CatNew = QuizArena.Admin.Client.Models.Categories.CategoryForm;
using CatOld = QuizArena.Admin.Client.Models.CategoryForm;
using CatProgression = QuizArena.Admin.Client.Models.Categories.ProgressionRule;

namespace QuizArena.Admin.Tests;

public sealed class CategoryTests
{
    private static CatOld ValidForm() => new("Matemáticas", "Álgebra y cálculo", "Matemáticas", "Secundaria", 12, 18, 3, []);
    private static CatNew ValidNewForm() => new("Matemáticas", "Álgebra", "Matemáticas", "Secundaria", 12, 18, 3, "Estudiantes", [], null, null, CatProgression.Linear);

    [Fact]
    public void Validate_NameTooShort_Fails()
    {
        var form = ValidForm() with { Name = "ab" };
        Assert.True(form.Validate().ContainsKey(nameof(CatOld.Name)));
    }

    [Fact]
    public void Validate_AreaTooShort_Fails()
    {
        var form = ValidForm() with { KnowledgeArea = "a" };
        Assert.True(form.Validate().ContainsKey(nameof(CatOld.KnowledgeArea)));
    }

    [Fact]
    public void Validate_AgeInverted_Fails()
    {
        var form = ValidForm() with { AgeMin = 20, AgeMax = 10 };
        Assert.True(form.Validate().ContainsKey(nameof(CatOld.AgeMax)));
    }

    [Fact]
    public void Validate_DifficultyOutOfRange_Fails()
    {
        var form = ValidForm() with { Difficulty = 0 };
        Assert.True(form.Validate().ContainsKey(nameof(CatOld.Difficulty)));
    }

    [Fact]
    public void Validate_TooManyTags_Fails()
    {
        var tags = Enumerable.Range(0, 11).Select(i => $"tag{i}").ToList();
        var form = ValidForm() with { Tags = tags };
        Assert.True(form.Validate().ContainsKey(nameof(CatOld.Tags)));
    }

    [Fact]
    public void Validate_DuplicateTags_Fails()
    {
        var form = ValidForm() with { Tags = ["tag", "TAG"] };
        Assert.True(form.Validate().ContainsKey(nameof(CatOld.Tags)));
    }

    [Fact]
    public void Validate_TargetAudienceTooShort_Fails()
    {
        var form = ValidNewForm() with { TargetAudience = "a" };
        Assert.True(form.Validate().ContainsKey(nameof(CatNew.TargetAudience)));
    }

    [Fact]
    public void Validate_ProgressionInvalid_Fails()
    {
        // New form's progression is enum, so test via old form's string progression
        var form = new CatOld("Valid Name", null, "Matemáticas", "Secundaria", 12, 18, 3, [], "General", "InvalidRule", null, null);
        Assert.True(form.Validate().ContainsKey(nameof(CatOld.ProgressionRule)));
    }

    [Fact]
    public void Validate_ValidForm_NoErrors()
    {
        Assert.Empty(ValidForm().Validate());
        Assert.Empty(ValidNewForm().Validate());
    }

    [Fact]
    public void Validate_ColorInvalid_Fails()
    {
        var form = ValidNewForm() with { Color = "not-hex" };
        Assert.True(form.Validate().ContainsKey(nameof(CatNew.Color)));
    }
}
