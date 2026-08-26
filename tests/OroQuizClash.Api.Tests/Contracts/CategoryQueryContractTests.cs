using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Repositories;
using BuildingBlocks.Kernel.Domain.Specifications;
using BuildingBlocks.Kernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using OroQuizClash.Application.Features.Categories;
using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Categories.ValueObjects;
using OroQuizClash.Infrastructure.Counters;
using OroQuizClash.Infrastructure.Persistence;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class CategoryQueryContractTests
{
    private static OroQuizClashDbContext CreateContext(string? dbName = null)
    {
        dbName ??= Guid.NewGuid().ToString();
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var ctx = new OroQuizClashDbContext(options, dispatcher);
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Category CreateCategory(
        string name,
        string knowledgeArea,
        string academicLevel,
        int ageMin,
        int ageMax,
        int difficulty,
        string statusName,
        string[] tags,
        bool seedActiveViaPublish = false)
    {
        var ka = new KnowledgeArea(knowledgeArea);
        var al = new AcademicLevel(academicLevel);
        var ar = new AgeRange(ageMin, ageMax);
        var dl = new DifficultyLevel(difficulty);
        var tg = new CategoryTags(tags);
        var pc = new PublishConfiguration(false);
        var result = Category.Create(name, $"desc {name}", ka, al, ar, dl, tg, pc, Guid.NewGuid());
        Assert.True(result.IsSuccess);
        var cat = result.Value;

        if (statusName == "ACTIVE")
        {
            // Bypass publish gate by Activate directly (DRAFT->ACTIVE allowed via Activate)
            var act = cat.Activate();
            Assert.True(act.IsSuccess);
        }
        else if (statusName == "INACTIVE")
        {
            cat.Activate();
            cat.Deactivate();
        }
        else if (statusName == "ARCHIVED")
        {
            cat.Archive();
        }

        return cat;
    }

    private static async Task<(OroQuizClashDbContext ctx, InMemoryQuestionCounter counter, IRepository<Category, CategoryId> repo)> Seed20Async()
    {
        var ctx = CreateContext();
        var repo = new EfRepository<Category, CategoryId>(ctx);
        var counter = new InMemoryQuestionCounter();
        // Seed 20 items with varied attributes for precise filtering
        var configs = new[]
        {
            new { Name="Cat01", KA="Humanidades", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="ACTIVE", Tags=new[]{"historia","secundaria"} },
            new { Name="Cat02", KA="Ciencias", AL="Universidad", AgeMin=18, AgeMax=25, Diff=4, Status="INACTIVE", Tags=new[]{"ciencia"} },
            new { Name="Cat03", KA="Humanidades", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=2, Status="ACTIVE", Tags=new[]{"álgebra","matemáticas"} },
            new { Name="Cat04", KA="Humanidades", AL="Primaria", AgeMin=6, AgeMax=11, Diff=1, Status="DRAFT", Tags=new[]{"historia"} },
            new { Name="Cat05", KA="Ciencias", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="ACTIVE", Tags=new[]{"física"} },
            new { Name="Cat06", KA="Humanidades", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="ACTIVE", Tags=new[]{"historia"} },
            new { Name="Cat07", KA="Matemáticas", AL="Secundaria", AgeMin=10, AgeMax=15, Diff=5, Status="ACTIVE", Tags=new[]{"álgebra"} },
            new { Name="Cat08", KA="Humanidades", AL="Universidad", AgeMin=18, AgeMax=30, Diff=4, Status="ARCHIVED", Tags=new[]{"filosofía"} },
            new { Name="Cat09", KA="Ciencias", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=2, Status="ACTIVE", Tags=new[]{"química"} },
            new { Name="Cat10", KA="Humanidades", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="INACTIVE", Tags=new[]{"literatura"} },
            new { Name="Cat11", KA="Humanidades", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="ACTIVE", Tags=new[]{"historia","álgebra"} },
            new { Name="Cat12", KA="Ciencias", AL="Primaria", AgeMin=6, AgeMax=11, Diff=1, Status="DRAFT", Tags=new[]{"biología"} },
            new { Name="Cat13", KA="Humanidades", AL="Secundaria", AgeMin=14, AgeMax=18, Diff=3, Status="ACTIVE", Tags=new[]{"geografía"} },
            new { Name="Cat14", KA="Matemáticas", AL="Universidad", AgeMin=18, AgeMax=25, Diff=4, Status="ACTIVE", Tags=new[]{"álgebra","cálculo"} },
            new { Name="Cat15", KA="Humanidades", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="ACTIVE", Tags=new[]{"historia"} },
            new { Name="Cat16", KA="Ciencias", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="ACTIVE", Tags=new[]{"ciencia","álgebra"} },
            new { Name="Cat17", KA="Humanidades", AL="Secundaria", AgeMin=10, AgeMax=12, Diff=2, Status="DRAFT", Tags=new[]{"historia"} },
            new { Name="Cat18", KA="Humanidades", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="ACTIVE", Tags=new[]{"arte"} },
            new { Name="Cat19", KA="Ciencias", AL="Universidad", AgeMin=18, AgeMax=25, Diff=5, Status="ACTIVE", Tags=new[]{"física","álgebra"} },
            new { Name="Cat20", KA="Humanidades", AL="Secundaria", AgeMin=13, AgeMax=17, Diff=3, Status="ACTIVE", Tags=new[]{"música"} },
        };

        foreach (var c in configs)
        {
            var cat = CreateCategory(c.Name, c.KA, c.AL, c.AgeMin, c.AgeMax, c.Diff, c.Status, c.Tags);
            await repo.AddAsync(cat, CancellationToken.None);
            // Seed validQuestionsCount for GetCategoryById
            if (c.Status == "ACTIVE")
                counter.Seed(cat.Id, 5);
            else
                counter.Seed(cat.Id, 2);
        }
        await ctx.SaveChangesAsync();
        return (ctx, counter, repo);
    }

    [Fact]
    public async Task GetCategories_Filter_KnowledgeArea_AcademicLevel_State_100PercentPrecision()
    {
        // Arrange: 3 categorías scenario from spec + 20 dataset ensures precision
        var (ctx, counter, repo) = await Seed20Async();
        var handler = new GetCategoriesHandler(repo, counter);

        // Act: filter Humanidades / Secundaria / ACTIVE should match precise subset
        // Expected in seeded data: Cat01, Cat03, Cat06, Cat11, Cat13, Cat15, Cat18, Cat20 = 8 ? but some have different age diff
        // We'll compute expected via independent LINQ then compare to handler result
        var query = new GetCategoriesQuery(
            KnowledgeArea: "Humanidades",
            AcademicLevel: "Secundaria",
            AgeMin: null,
            AgeMax: null,
            DifficultyLevel: null,
            State: "ACTIVE",
            Tag: null,
            Page: 1,
            PageSize: 20);

        var result = await handler.HandleAsync(query, CancellationToken.None);

        // Assert: 100% precision - every returned item must match filter
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
        foreach (var item in result.Value.Items)
        {
            Assert.Equal("Humanidades", item.KnowledgeArea);
            Assert.Equal("Secundaria", item.AcademicLevel);
            Assert.Equal("ACTIVE", item.Status);
        }
        // Cross-check total matches EF spec count directly
        var countSpec = CategoryFilterSpecification.ForCount("Humanidades", "Secundaria", null, "ACTIVE", null, null, null);
        var expectedTotal = await repo.CountAsync(countSpec, CancellationToken.None);
        Assert.Equal(expectedTotal, result.Value.Total);
        Assert.Equal(expectedTotal, result.Value.Items.Count);
        // Verify precision is 100% by ensuring no false positives via separate in-memory evaluation
        var listSpec = new CategoryFilterSpecification("Humanidades", "Secundaria", null, "ACTIVE", null, null, null, 1, 100);
        var list = await repo.ListAsync(listSpec, CancellationToken.None);
        Assert.Equal(list.Count, result.Value.Items.Count);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetCategories_Filter_Tag_Includes()
    {
        var (ctx, counter, repo) = await Seed20Async();
        var handler = new GetCategoriesHandler(repo, counter);

        var query = new GetCategoriesQuery(null, null, null, null, null, null, "álgebra", 1, 20);
        var result = await handler.HandleAsync(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotEmpty(result.Value.Items);
        foreach (var item in result.Value.Items)
        {
            Assert.Contains("álgebra", item.Tags);
        }
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetCategories_Pagination_Page_PageSize()
    {
        var (ctx, counter, repo) = await Seed20Async();
        var handler = new GetCategoriesHandler(repo, counter);

        var qPage1 = new GetCategoriesQuery(null, null, null, null, null, null, null, 1, 5);
        var qPage2 = new GetCategoriesQuery(null, null, null, null, null, null, null, 2, 5);
        var qPage3 = new GetCategoriesQuery(null, null, null, null, null, null, null, 3, 5);

        var r1 = await handler.HandleAsync(qPage1, CancellationToken.None);
        var r2 = await handler.HandleAsync(qPage2, CancellationToken.None);
        var r3 = await handler.HandleAsync(qPage3, CancellationToken.None);

        Assert.True(r1.IsSuccess && r2.IsSuccess && r3.IsSuccess);
        Assert.Equal(20, r1.Value.Total);
        Assert.Equal(5, r1.Value.Items.Count);
        Assert.Equal(5, r2.Value.Items.Count);
        Assert.Equal(5, r3.Value.Items.Count);
        Assert.Equal(1, r1.Value.Page);
        Assert.Equal(2, r2.Value.Page);
        // Items across pages must be distinct (paging without duplication)
        var ids1 = r1.Value.Items.Select(i => i.Id).ToHashSet();
        var ids2 = r2.Value.Items.Select(i => i.Id).ToHashSet();
        Assert.Empty(ids1.Intersect(ids2));
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetCategories_Filter_AgeRange_And_Difficulty()
    {
        var (ctx, counter, repo) = await Seed20Async();
        var handler = new GetCategoriesHandler(repo, counter);

        // Filter by age overlap 13-17 and difficulty 3
        var query = new GetCategoriesQuery(null, null, 13, 17, 3, null, null, 1, 20);
        var result = await handler.HandleAsync(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        foreach (var item in result.Value.Items)
        {
            Assert.True(item.AgeMax >= 13 && item.AgeMin <= 17);
            Assert.Equal(3, item.DifficultyLevel);
        }
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetCategoryById_Returns_ValidQuestionsCount()
    {
        var (ctx, counter, repo) = await Seed20Async();
        // Pick one ACTIVE category (should have 5 valid)
        var all = await repo.ListAsync(new CategoryFilterSpecification(state: "ACTIVE", pageSize: 1), CancellationToken.None);
        Assert.NotEmpty(all);
        var target = all.First();
        // Ensure counter seeded 5 for ACTIVE
        var handler = new GetCategoryByIdHandler(repo, counter);
        var result = await handler.HandleAsync(new GetCategoryByIdQuery(target.Id.Value), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(target.Id.Value, result.Value.Id);
        Assert.Equal(5, result.Value.ValidQuestionsCount);
        Assert.Equal("ACTIVE", result.Value.Status);

        // INACTIVE should have 2
        var inactiveList = await repo.ListAsync(new CategoryFilterSpecification(state: "INACTIVE", pageSize: 1), CancellationToken.None);
        if (inactiveList.Count > 0)
        {
            var inactive = inactiveList.First();
            var r2 = await handler.HandleAsync(new GetCategoryByIdQuery(inactive.Id.Value), CancellationToken.None);
            Assert.True(r2.IsSuccess);
            Assert.Equal(2, r2.Value.ValidQuestionsCount);
        }
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetCategoryById_NotFound_Returns404()
    {
        var (ctx, counter, repo) = await Seed20Async();
        var handler = new GetCategoryByIdHandler(repo, counter);
        var result = await handler.HandleAsync(new GetCategoryByIdQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Equal("CategoryNotFound", result.Error.Code);
        Assert.Equal(BuildingBlocks.Kernel.Domain.Results.ErrorType.NotFound, result.Error.Type);
        await ctx.DisposeAsync();
    }

    [Fact]
    public async Task GetCategories_AsNoTracking_Pagination_Skip_Take_Applied()
    {
        // Verify specification uses AsNoTracking + Skip/Take
        var spec = new CategoryFilterSpecification(page: 2, pageSize: 7);
        Assert.True(spec.AsNoTracking);
        Assert.Equal(7, spec.Take);
        Assert.Equal(7, spec.Skip);
    }

    [Fact]
    public void Validator_InvalidPage_Fails()
    {
        var validator = new GetCategoriesValidator();
        var q = new GetCategoriesQuery(null, null, null, null, null, null, null, 0, 200);
        var failures = validator.ValidateAsync(q, CancellationToken.None).Result;
        Assert.Contains(failures, f => f.PropertyName == "Page");
        Assert.Contains(failures, f => f.PropertyName == "PageSize");
    }
}