using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Categories.ValueObjects;

namespace OroQuizClash.Domain.Tests.Categories;

public sealed class CategoryCreateUpdateTests
{
    private static (KnowledgeArea ka, AcademicLevel al, AgeRange ar, DifficultyLevel dl, CategoryTags tags, PublishConfiguration pc) ValidVOs(
        string[]? tags = null)
    {
        var ka = new KnowledgeArea("Humanidades");
        var al = new AcademicLevel("Secundaria");
        var ar = new AgeRange(13, 17);
        var dl = new DifficultyLevel(3);
        var tg = tags == null ? new CategoryTags(["historia", "secundaria"]) : new CategoryTags(tags);
        var pc = new PublishConfiguration(false);
        return (ka, al, ar, dl, tg, pc);
    }

    [Fact]
    public void Create_WithValidData_Succeeds_DRAFT()
    {
        // Arrange
        var (ka, al, ar, dl, tags, pc) = ValidVOs();
        // Act
        var result = Category.Create("Historia Universal", "Desde prehistoria", ka, al, ar, dl, tags, pc, Guid.NewGuid());
        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Historia Universal", result.Value.Name);
        Assert.Equal(CategoryStatus.Draft, result.Value.Status);
        Assert.Equal(13, result.Value.AgeRange.Min);
        Assert.Equal(17, result.Value.AgeRange.Max);
    }

    [Fact]
    public void Create_WithEmptyName_Fails_InvalidName()
    {
        // Arrange
        var (ka, al, ar, dl, tags, pc) = ValidVOs();
        // Act
        var result = Category.Create("", "desc", ka, al, ar, dl, tags, pc, Guid.NewGuid());
        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryConfiguration.InvalidName", result.Error.Code);
    }

    [Fact]
    public void Create_WithShortName_Fails()
    {
        // Arrange
        var (ka, al, ar, dl, tags, pc) = ValidVOs();
        // Act
        var result = Category.Create("ab", "desc", ka, al, ar, dl, tags, pc, Guid.NewGuid());
        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryConfiguration.InvalidName", result.Error.Code);
    }

    [Fact]
    public void AgeRange_Invertido_Throws()
    {
        // Arrange & Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new AgeRange(17, 13));
        Assert.Contains("Min must be <= Max", ex.Message);
    }

    [Fact]
    public void Category_Create_WithInvertidoAgeRange_FailsViaVO()
    {
        // Direct VO throws; simulate via handler pattern: construction exception expected
        // Arrange
        AgeRange? ar = null;
        Exception? caught = null;
        try { ar = new AgeRange(17, 13); } catch (Exception ex) { caught = ex; }
        // Assert
        Assert.NotNull(caught);
        Assert.IsType<ArgumentException>(caught);
    }

    [Fact]
    public void Tags_Deduplicados_Normalizados()
    {
        // Arrange
        var tags = new CategoryTags(["Historia", " historia ", "HISTORIA", "Secundaria"]);
        // Act
        var normalized = tags.Tags;
        // Assert
        Assert.Equal(2, normalized.Count);
        Assert.Contains("historia", normalized);
        Assert.Contains("secundaria", normalized);
    }

    [Fact]
    public void Tags_Exceeds10_Throws()
    {
        // Arrange
        var many = Enumerable.Range(0, 11).Select(i => $"tag{i:00}").ToArray();
        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => new CategoryTags(many));
        Assert.Contains("more than 10 tags", ex.Message);
    }

    [Fact]
    public void Update_En_ARCHIVED_Fails_InvalidCategoryState()
    {
        // Arrange
        var (ka, al, ar, dl, tags, pc) = ValidVOs();
        var createResult = Category.Create("Historia Universal", "desc", ka, al, ar, dl, tags, pc, Guid.NewGuid());
        Assert.True(createResult.IsSuccess);
        var category = createResult.Value;
        // Archive it
        var archiveResult = category.Archive();
        Assert.True(archiveResult.IsSuccess);
        Assert.Equal(CategoryStatus.Archived, category.Status);

        // Act
        var updateResult = category.Update("Nuevo Nombre", "desc2", ka, al, ar, dl, tags, pc);

        // Assert
        Assert.True(updateResult.IsFailure);
        Assert.Equal("InvalidCategoryState", updateResult.Error.Code);
    }

    [Fact]
    public void Update_En_DRAFT_Succeeds()
    {
        // Arrange
        var (ka, al, ar, dl, tags, pc) = ValidVOs();
        var cat = Category.Create("Historia Universal", "desc", ka, al, ar, dl, tags, pc, Guid.NewGuid()).Value;
        var newKa = new KnowledgeArea("Ciencias");
        var newTags = new CategoryTags(["nuevo"]);

        // Act
        var result = cat.Update("Nuevo Nombre", "Nueva Desc", newKa, al, ar, dl, newTags, pc);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Nuevo Nombre", cat.Name);
        Assert.Equal("Ciencias", cat.KnowledgeArea.Value);
        Assert.Single(cat.Tags.Tags);
    }

    [Fact]
    public void Update_En_ACTIVE_Fails()
    {
        // Arrange
        var (ka, al, ar, dl, tags, pc) = ValidVOs();
        var cat = Category.Create("Historia Universal", "desc", ka, al, ar, dl, tags, pc, Guid.NewGuid()).Value;
        cat.Activate();
        Assert.Equal(CategoryStatus.Active, cat.Status);

        // Act
        var result = cat.Update("Otro", "desc", ka, al, ar, dl, tags, pc);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
    }

    [Fact]
    public void Create_WithTooLongDescription_Fails()
    {
        // Arrange
        var (ka, al, ar, dl, tags, pc) = ValidVOs();
        var longDesc = new string('x', 501);
        // Act
        var result = Category.Create("Historia Universal", longDesc, ka, al, ar, dl, tags, pc, Guid.NewGuid());
        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("Description", result.Error.Description);
    }
}