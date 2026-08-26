using BuildingBlocks.Kernel.Domain.Repositories;

using NSubstitute;

using OroQuizClash.Application.Features.Categories;
using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Application.Tests.Features.Categories;

public sealed class CategoryHandlersTests
{
    private static IRepository<Category, CategoryId> MockRepo(Category? existing = null)
    {
        var repo = Substitute.For<IRepository<Category, CategoryId>>();
        if (existing != null)
        {
            repo.GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>()).Returns(existing);
        }
        else
        {
            repo.GetByIdAsync(Arg.Any<CategoryId>(), Arg.Any<CancellationToken>()).Returns((Category?)null);
        }
        return repo;
    }

    [Fact]
    public async Task CreateCategoryHandler_WithValidCommand_Succeeds()
    {
        // Arrange
        var repo = MockRepo();
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new CreateCategoryHandler(repo, uow);
        var cmd = new CreateCategoryCommand(
            Name: "Historia Universal",
            Description: "Desde prehistoria",
            KnowledgeArea: "Humanidades",
            AcademicLevel: "Secundaria",
            AgeMin: 13,
            AgeMax: 17,
            DifficultyLevel: 3,
            Tags: new List<string> { "historia", "secundaria" },
            RequiresModeration: false);

        // Act
        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        Assert.Equal("Historia Universal", result.Value.Name);
        Assert.Equal("DRAFT", result.Value.Status);
        await repo.Received(1).AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCategoryHandler_WithInvalidAgeRange_Fails()
    {
        // Arrange
        var repo = MockRepo();
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new CreateCategoryHandler(repo, uow);
        var cmd = new CreateCategoryCommand(
            Name: "Historia Universal",
            Description: "desc",
            KnowledgeArea: "Humanidades",
            AcademicLevel: "Secundaria",
            AgeMin: 17,
            AgeMax: 13,
            DifficultyLevel: 3,
            Tags: new List<string> { "historia" });

        // Act
        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        // Should be InvalidAgeRange error via VO handling
        Assert.Contains("InvalidAgeRange", result.Error.Code);
        await repo.DidNotReceive().AddAsync(Arg.Any<Category>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateCategoryHandler_WithTooManyTags_Fails()
    {
        // Arrange
        var repo = MockRepo();
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new CreateCategoryHandler(repo, uow);
        var manyTags = Enumerable.Range(0, 11).Select(i => $"tag{i:00}").ToList();
        var cmd = new CreateCategoryCommand(
            Name: "Historia Universal",
            Description: "desc",
            KnowledgeArea: "Humanidades",
            AcademicLevel: "Secundaria",
            AgeMin: 10,
            AgeMax: 15,
            DifficultyLevel: 2,
            Tags: manyTags);

        // Act
        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Contains("InvalidTags", result.Error.Code);
    }

    [Fact]
    public async Task UpdateCategoryHandler_WithExistingDraft_Succeeds()
    {
        // Arrange
        var existing = CreateDraftCategory("Old Name");
        var repo = MockRepo(existing);
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new UpdateCategoryHandler(repo, uow);
        var cmd = new UpdateCategoryCommand(
            Id: existing.Id.Value,
            Name: "Nuevo Nombre",
            Description: "Nueva desc",
            KnowledgeArea: "Ciencias",
            AcademicLevel: "Universidad",
            AgeMin: 18,
            AgeMax: 25,
            DifficultyLevel: 4,
            Tags: new List<string> { "nuevo" });

        // Act
        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Nuevo Nombre", result.Value.Name);
        Assert.Equal("Ciencias", result.Value.KnowledgeArea);
        repo.Received(1).Update(Arg.Any<Category>());
        await uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateCategoryHandler_WithNotFound_Fails()
    {
        // Arrange
        var repo = MockRepo(null);
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new UpdateCategoryHandler(repo, uow);
        var cmd = new UpdateCategoryCommand(
            Id: Guid.NewGuid(),
            Name: "Nombre",
            Description: "desc",
            KnowledgeArea: "Humanidades",
            AcademicLevel: "Secundaria",
            AgeMin: 13,
            AgeMax: 17,
            DifficultyLevel: 3,
            Tags: new List<string> { "historia" });

        // Act
        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("CategoryNotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateCategoryHandler_OnArchived_Fails_InvalidState()
    {
        // Arrange
        var existing = CreateDraftCategory("Old");
        existing.Archive(); // now ARCHIVED
        var repo = MockRepo(existing);
        var uow = Substitute.For<IUnitOfWork>();
        var handler = new UpdateCategoryHandler(repo, uow);
        var cmd = new UpdateCategoryCommand(
            Id: existing.Id.Value,
            Name: "Nuevo",
            Description: "desc",
            KnowledgeArea: "Humanidades",
            AcademicLevel: "Secundaria",
            AgeMin: 13,
            AgeMax: 17,
            DifficultyLevel: 3,
            Tags: new List<string> { "historia" });

        // Act
        var result = await handler.HandleAsync(cmd, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("InvalidCategoryState", result.Error.Code);
    }

    [Fact]
    public async Task CreateCategoryValidator_ValidPasses()
    {
        // Arrange
        var validator = new CreateCategoryValidator();
        var cmd = new CreateCategoryCommand("Historia Universal", "desc", "Humanidades", "Secundaria", 13, 17, 3, new List<string> { "historia" });

        // Act
        var failures = await validator.ValidateAsync(cmd, CancellationToken.None);

        // Assert
        Assert.Empty(failures);
    }

    [Fact]
    public async Task CreateCategoryValidator_InvalidName_Fails()
    {
        // Arrange
        var validator = new CreateCategoryValidator();
        var cmd = new CreateCategoryCommand("ab", "desc", "Humanidades", "Secundaria", 13, 17, 3, null);

        // Act
        var failures = await validator.ValidateAsync(cmd, CancellationToken.None);

        // Assert
        Assert.NotEmpty(failures);
        Assert.Contains(failures, f => f.PropertyName == "Name");
    }

    private static Category CreateDraftCategory(string name)
    {
        var ka = new OroQuizClash.Domain.Categories.ValueObjects.KnowledgeArea("Humanidades");
        var al = new OroQuizClash.Domain.Categories.ValueObjects.AcademicLevel("Secundaria");
        var ar = new OroQuizClash.Domain.Categories.ValueObjects.AgeRange(13, 17);
        var dl = new OroQuizClash.Domain.Categories.ValueObjects.DifficultyLevel(3);
        var tags = new OroQuizClash.Domain.Categories.ValueObjects.CategoryTags(["historia"]);
        var pc = new OroQuizClash.Domain.Categories.ValueObjects.PublishConfiguration(false);
        var result = Category.Create(name, "desc", ka, al, ar, dl, tags, pc, Guid.NewGuid());
        Assert.True(result.IsSuccess);
        return result.Value;
    }
}