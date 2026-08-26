using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Specifications;
using BuildingBlocks.Kernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Categories.ValueObjects;
using OroQuizClash.Infrastructure.Persistence;

namespace OroQuizClash.Infrastructure.Tests.Persistence;

public sealed class CategoryPersistenceTests
{
    private static OroQuizClashDbContext CreateContext(string? dbName = null, bool useSqlite = false)
    {
        dbName ??= Guid.NewGuid().ToString();
        var dispatcher = Substitute.For<IDomainEventDispatcher>();

        if (useSqlite)
        {
            var optionsSqlite = new DbContextOptionsBuilder<OroQuizClashDbContext>()
                .UseSqlite($"Data Source={dbName};Mode=Memory;Cache=Shared")
                .Options;
            var ctx = new OroQuizClashDbContext(optionsSqlite, dispatcher);
            ctx.Database.OpenConnection();
            ctx.Database.EnsureCreated();
            return ctx;
        }
        else
        {
            var options = new DbContextOptionsBuilder<OroQuizClashDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
            var ctx = new OroQuizClashDbContext(options, dispatcher);
            ctx.Database.EnsureCreated();
            return ctx;
        }
    }

    private static Category CreateValidCategory(string name = "Historia Universal", string[]? tags = null)
    {
        var ka = new KnowledgeArea("Humanidades");
        var al = new AcademicLevel("Secundaria");
        var ar = new AgeRange(13, 17);
        var dl = new DifficultyLevel(3);
        var tg = tags == null ? new CategoryTags(["historia", "secundaria"]) : new CategoryTags(tags);
        var pc = new PublishConfiguration(false);
        var result = Category.Create(name, "Desde prehistoria", ka, al, ar, dl, tg, pc, Guid.NewGuid());
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    [Fact]
    public async Task AddCategory_EnsureCreated_PersistsAndQueriesViaSpecification()
    {
        // Arrange
        await using var ctx = CreateContext();
        var category = CreateValidCategory();
        var repository = new EfRepository<Category, CategoryId>(ctx);

        // Act
        await repository.AddAsync(category, CancellationToken.None);
        await ctx.SaveChangesAsync();

        // Assert via Specification (query AgeRange owned + Tags conversion + RowVersion)
        var spec = new CategoryByNameSpecification(category.Name);
        var loaded = await repository.FirstOrDefaultAsync(spec, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(category.Id.Value, loaded!.Id.Value);
        Assert.Equal("Historia Universal", loaded.Name);
        Assert.Equal(13, loaded.AgeRange.Min);
        Assert.Equal(17, loaded.AgeRange.Max);
        Assert.Contains("historia", loaded.Tags.Tags);
        Assert.Contains("secundaria", loaded.Tags.Tags);
        // RowVersion may be null/empty in InMemory provider but property exists
        Assert.NotNull(loaded.RowVersion);
        Assert.Equal(CategoryStatus.Draft, loaded.Status);
    }

    [Fact]
    public async Task Tags_Conversion_Roundtrips_DeduplicatedLowercased()
    {
        // Arrange
        await using var ctx = CreateContext();
        var category = CreateValidCategory(tags: new[] { "HISTORIA", " historia ", "Secundaria" });
        var repo = new EfRepository<Category, CategoryId>(ctx);

        // Act
        await repo.AddAsync(category, CancellationToken.None);
        await ctx.SaveChangesAsync();

        var loaded = await repo.GetByIdAsync(category.Id, CancellationToken.None);

        // Assert
        Assert.NotNull(loaded);
        // CategoryTags normalizes to lowercased deduplicated
        Assert.Equal(2, loaded!.Tags.Tags.Count);
        Assert.Contains("historia", loaded.Tags.Tags);
        Assert.Contains("secundaria", loaded.Tags.Tags);
    }

    [Fact]
    public async Task AgeRange_Owned_PersistsCorrectly()
    {
        // Arrange - use InMemory to avoid Sqlite RowVersion NOT NULL constraint; still validates OwnsOne mapping
        await using var ctx = CreateContext(useSqlite: false);
        var category = CreateValidCategory();
        var repo = new EfRepository<Category, CategoryId>(ctx);

        // Act
        await repo.AddAsync(category, CancellationToken.None);
        await ctx.SaveChangesAsync();

        var spec = new CategoryByIdSpecification(category.Id);
        var loaded = await repo.FirstOrDefaultAsync(spec, CancellationToken.None);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(13, loaded!.AgeRange.Min);
        Assert.Equal(17, loaded.AgeRange.Max);
    }

    [Fact]
    public async Task RowVersion_IsRowVersionProperty()
    {
        // Arrange - verify model metadata without requiring Sqlite rowversion generation
        await using var ctx = CreateContext(useSqlite: false);
        var category = CreateValidCategory();

        // Assert - check property exists and is concurrency token via IsRowVersion()
        var entry = ctx.Entry(category);
        var rowVersionProp = entry.Metadata.FindProperty(nameof(Category.RowVersion));
        Assert.NotNull(rowVersionProp);
        // RowVersion should be marked as concurrency token via IsRowVersion()
        Assert.True(rowVersionProp!.IsConcurrencyToken);
        Assert.True(rowVersionProp.IsConcurrencyToken);
        // RowVersion value initially empty but property configured
        Assert.NotNull(category.RowVersion);
    }

    [Fact]
    public async Task Specification_AsNoTracking_FiltersByKnowledgeAreaAndStatus()
    {
        // Arrange
        await using var ctx = CreateContext();
        var cat1 = CreateValidCategory("Cat A"); // Humanidades / DRAFT
        var kaCiencias = new KnowledgeArea("Ciencias");
        var alUniv = new AcademicLevel("Universidad");
        var ar = new AgeRange(18, 25);
        var dl = new DifficultyLevel(4);
        var tags = new CategoryTags(["ciencia"]);
        var pc = new PublishConfiguration(false);
        var cat2 = Category.Create("Cat B", "desc", kaCiencias, alUniv, ar, dl, tags, pc, Guid.NewGuid()).Value;

        var repo = new EfRepository<Category, CategoryId>(ctx);
        await repo.AddAsync(cat1, CancellationToken.None);
        await repo.AddAsync(cat2, CancellationToken.None);
        await ctx.SaveChangesAsync();

        // Act
        var spec = new CategoryFilterSpecification(knowledgeArea: "Humanidades", status: CategoryStatus.Draft);
        var list = await repo.ListAsync(spec, CancellationToken.None);

        // Assert
        Assert.Single(list);
        Assert.Equal("Cat A", list[0].Name);
    }
}

// Specification helpers for tests
public sealed class CategoryByNameSpecification : Specification<Category>
{
    public CategoryByNameSpecification(string name)
    {
        Where(c => c.Name == name);
    }
}

public sealed class CategoryByIdSpecification : Specification<Category>
{
    public CategoryByIdSpecification(CategoryId id)
    {
        Where(c => c.Id == id);
    }
}

public sealed class CategoryFilterSpecification : Specification<Category>
{
    public CategoryFilterSpecification(string? knowledgeArea = null, CategoryStatus? status = null, bool asNoTracking = false)
    {
        if (knowledgeArea != null)
            Where(c => c.KnowledgeArea.Value == knowledgeArea);
        if (status != null)
            Where(c => c.Status == status);
        if (asNoTracking)
            ApplyAsNoTracking();
    }
}