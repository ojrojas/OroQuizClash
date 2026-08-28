using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Tests;

/// <summary>T046: QuestionForm/CategoryForm invariants.</summary>
public sealed class ContentServiceTests
{
    [Fact]
    public void QuestionForm_ExactlyFourOptions_Required()
    {
        var form = ValidQuestionForm() with { Options = [new OptionForm("A", true), new OptionForm("B", false)] };
        Assert.True(form.Validate().ContainsKey(nameof(QuestionForm.Options)));
    }

    [Fact]
    public void QuestionForm_ExactlyOneCorrect_Required()
    {
        var form = ValidQuestionForm() with { Options = [new OptionForm("A", true), new OptionForm("B", true), new OptionForm("C", false), new OptionForm("D", false)] };
        Assert.True(form.Validate().ContainsKey(nameof(QuestionForm.Options)));
    }

    [Fact]
    public void QuestionForm_ZeroCorrect_Fails()
    {
        var form = ValidQuestionForm() with { Options = [new OptionForm("A", false), new OptionForm("B", false), new OptionForm("C", false), new OptionForm("D", false)] };
        Assert.True(form.Validate().ContainsKey(nameof(QuestionForm.Options)));
    }

    [Fact]
    public void QuestionForm_TextTooShort_Fails()
    {
        var form = ValidQuestionForm() with { Text = "Short" };
        Assert.True(form.Validate().ContainsKey(nameof(QuestionForm.Text)));
    }

    [Fact]
    public void QuestionForm_Valid_HasNoErrors() => Assert.Empty(ValidQuestionForm().Validate());

    [Fact]
    public void CategoryForm_AgeMaxMustBeAtLeastMin()
    {
        var form = ValidCategoryForm() with { AgeMin = 20, AgeMax = 10 };
        Assert.True(form.Validate().ContainsKey(nameof(CategoryForm.AgeMax)));
    }

    [Fact]
    public void CategoryForm_TooManyTags_Fails()
    {
        var tags = Enumerable.Range(0, 11).Select(i => $"tag{i}").ToList();
        var form = ValidCategoryForm() with { Tags = tags };
        Assert.True(form.Validate().ContainsKey(nameof(CategoryForm.Tags)));
    }

    [Fact]
    public void CategoryForm_TagTooShort_Fails()
    {
        var form = ValidCategoryForm() with { Tags = ["a"] };
        Assert.True(form.Validate().ContainsKey(nameof(CategoryForm.Tags)));
    }

    [Fact]
    public void CategoryForm_Valid_HasNoErrors() => Assert.Empty(ValidCategoryForm().Validate());

    private static QuestionForm ValidQuestionForm() => new(
        Text: "What is the capital of France with enough length?",
        CategoryId: Guid.NewGuid(),
        Difficulty: 2,
        AcademicLevel: "Bachelor",
        AgeMin: 10,
        AgeMax: 20,
        Options: [new OptionForm("Paris", true), new OptionForm("London", false), new OptionForm("Berlin", false), new OptionForm("Rome", false)]);

    private static CategoryForm ValidCategoryForm() => new(
        Name: "Science", Description: null, KnowledgeArea: "Physics", AcademicLevel: "High", AgeMin: 10, AgeMax: 18, Difficulty: 2, Tags: ["space", "energy"]);
}
