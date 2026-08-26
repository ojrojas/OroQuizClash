using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Specifications;
using BuildingBlocks.Kernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Categories.ValueObjects;
using OroQuizClash.Infrastructure.Persistence;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Infrastructure.Tests.Specifications;

public sealed class CategoryFilterSpecificationTests
{
    private static Category Make(
        string name,
        string ka,
        string al,
        int ageMin,
        int ageMax,
        int diff,
        string status,
        string[] tags)
    {
        var k = new KnowledgeArea(ka);
        var a = new AcademicLevel(al);
        var ar = new AgeRange(ageMin, ageMax);
        var dl = new DifficultyLevel(diff);
        var t = new CategoryTags(tags);
        var pc = new PublishConfiguration(false);
        var r = Category.Create(name, "desc", k, a, ar, dl, t, pc, Guid.NewGuid());
        Assert.True(r.IsSuccess);
        var cat = r.Value;
        if (status == "ACTIVE") cat.Activate();
        else if (status == "INACTIVE") { cat.Activate(); cat.Deactivate(); }
        else if (status == "ARCHIVED") cat.Archive();
        return cat;
    }

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

    [Fact]
    public void IsSatisfiedBy_KnowledgeArea_TrueOnlyForMatching()
    {
        var cat = Make("C1", "Humanidades", "Secundaria", 13, 17, 3, "DRAFT", new[] { "historia" });
        var specMatch = new CategoryFilterSpecification(knowledgeArea: "Humanidades", paginate: false);
        var specNoMatch = new CategoryFilterSpecification(knowledgeArea: "Ciencias", paginate: false);

        Assert.True(specMatch.IsSatisfiedBy(cat));
        Assert.False(specNoMatch.IsSatisfiedBy(cat));
    }

    [Fact]
    public void IsSatisfiedBy_AcademicLevel()
    {
        var cat = Make("C1", "Humanidades", "Secundaria", 13, 17, 3, "DRAFT", new[] { "historia" });
        Assert.True(new CategoryFilterSpecification(academicLevel: "Secundaria", paginate: false).IsSatisfiedBy(cat));
        Assert.False(new CategoryFilterSpecification(academicLevel: "Universidad", paginate: false).IsSatisfiedBy(cat));
    }

    [Fact]
    public void IsSatisfiedBy_State()
    {
        var active = Make("A", "Humanidades", "Secundaria", 13, 17, 3, "ACTIVE", new[] { "historia" });
        var draft = Make("D", "Humanidades", "Secundaria", 13, 17, 3, "DRAFT", new[] { "historia" });

        var activeSpec = new CategoryFilterSpecification(state: "ACTIVE", paginate: false);
        Assert.True(activeSpec.IsSatisfiedBy(active));
        Assert.False(activeSpec.IsSatisfiedBy(draft));

        var draftSpec = new CategoryFilterSpecification(state: "DRAFT", paginate: false);
        Assert.True(draftSpec.IsSatisfiedBy(draft));
    }

    [Fact]
    public void IsSatisfiedBy_Tag_NormalizedLower()
    {
        var cat = Make("C1", "Humanidades", "Secundaria", 13, 17, 3, "DRAFT", new[] { "Álgebra", "matemáticas" });
        // tags normalized to lower
        var spec = new CategoryFilterSpecification(tag: "álgebra", paginate: false);
        Assert.True(spec.IsSatisfiedBy(cat));

        var specUpper = new CategoryFilterSpecification(tag: "ÁLGEBRA", paginate: false);
        Assert.True(specUpper.IsSatisfiedBy(cat));

        var specNo = new CategoryFilterSpecification(tag: "física", paginate: false);
        Assert.False(specNo.IsSatisfiedBy(cat));
    }

    [Fact]
    public void IsSatisfiedBy_AgeRange_Overlap()
    {
        var cat = Make("C1", "Humanidades", "Secundaria", 10, 15, 3, "DRAFT", new[] { "historia" });
        // query ageMin 13 => cat Max 15 >=13 true, Min 10 <= AgeMax maybe
        Assert.True(new CategoryFilterSpecification(ageMin: 13, paginate: false).IsSatisfiedBy(cat));
        Assert.True(new CategoryFilterSpecification(ageMax: 12, paginate: false).IsSatisfiedBy(cat)); // Min 10 <=12 true
        Assert.False(new CategoryFilterSpecification(ageMin: 16, paginate: false).IsSatisfiedBy(cat)); // Max 15 >=16 false
        Assert.False(new CategoryFilterSpecification(ageMax: 9, paginate: false).IsSatisfiedBy(cat)); // Min 10 <=9 false

        // Both
        Assert.True(new CategoryFilterSpecification(ageMin: 12, ageMax: 14, paginate: false).IsSatisfiedBy(cat));
        Assert.False(new CategoryFilterSpecification(ageMin: 16, ageMax: 20, paginate: false).IsSatisfiedBy(cat));
    }

    [Fact]
    public void IsSatisfiedBy_DifficultyLevel()
    {
        var cat = Make("C1", "Humanidades", "Secundaria", 13, 17, 3, "DRAFT", new[] { "historia" });
        Assert.True(new CategoryFilterSpecification(difficultyLevel: 3, paginate: false).IsSatisfiedBy(cat));
        Assert.False(new CategoryFilterSpecification(difficultyLevel: 5, paginate: false).IsSatisfiedBy(cat));
    }

    [Fact]
    public void IsSatisfiedBy_Combined_And()
    {
        var cat = Make("C1", "Humanidades", "Secundaria", 13, 17, 3, "ACTIVE", new[] { "historia", "álgebra" });
        var spec = new CategoryFilterSpecification(
            knowledgeArea: "Humanidades",
            academicLevel: "Secundaria",
            difficultyLevel: 3,
            state: "ACTIVE",
            tag: "álgebra",
            ageMin: 13,
            ageMax: 17,
            page: 1,
            pageSize: 20,
            paginate: false);
        Assert.True(spec.IsSatisfiedBy(cat));

        var specFail = new CategoryFilterSpecification(
            knowledgeArea: "Ciencias",
            academicLevel: "Secundaria",
            state: "ACTIVE",
            paginate: false);
        Assert.False(specFail.IsSatisfiedBy(cat));
    }

    [Fact]
    public async Task EFTranslation_FiltersCorrectly()
    {
        await using var ctx = CreateContext();
        var repo = new EfRepository<Category, CategoryId>(ctx);
        var c1 = Make("Human Sec Active", "Humanidades", "Secundaria", 13, 17, 3, "ACTIVE", new[] { "historia" });
        var c2 = Make("Ciencias Univ Inactive", "Ciencias", "Universidad", 18, 25, 4, "INACTIVE", new[] { "ciencia" });
        var c3 = Make("Human Sec Active 2", "Humanidades", "Secundaria", 13, 17, 2, "ACTIVE", new[] { "álgebra" });
        await repo.AddAsync(c1, CancellationToken.None);
        await repo.AddAsync(c2, CancellationToken.None);
        await repo.AddAsync(c3, CancellationToken.None);
        await ctx.SaveChangesAsync();

        var spec = new CategoryFilterSpecification("Humanidades", "Secundaria", null, "ACTIVE", null, null, null, 1, 10);
        var list = await repo.ListAsync(spec, CancellationToken.None);
        Assert.Equal(2, list.Count);
        Assert.All(list, c => Assert.Equal("Humanidades", c.KnowledgeArea.Value));
        Assert.All(list, c => Assert.Equal("ACTIVE", c.Status.Name));
    }

    [Fact]
    public async Task EFTranslation_TagFilter()
    {
        await using var ctx = CreateContext();
        var repo = new EfRepository<Category, CategoryId>(ctx);
        var c1 = Make("C1", "Humanidades", "Secundaria", 13, 17, 3, "ACTIVE", new[] { "álgebra", "matemáticas" });
        var c2 = Make("C2", "Humanidades", "Secundaria", 13, 17, 3, "ACTIVE", new[] { "historia" });
        await repo.AddAsync(c1, CancellationToken.None);
        await repo.AddAsync(c2, CancellationToken.None);
        await ctx.SaveChangesAsync();

        var spec = new CategoryFilterSpecification(tag: "álgebra", page: 1, pageSize: 10);
        var list = await repo.ListAsync(spec, CancellationToken.None);
        Assert.Single(list);
        Assert.Equal("C1", list[0].Name);
    }

    [Fact]
    public async Task EFTranslation_AgeRange_Pagination()
    {
        await using var ctx = CreateContext();
        var repo = new EfRepository<Category, CategoryId>(ctx);
        for (int i = 0; i < 6; i++)
        {
            var cat = Make($"Cat{i:00}", "Humanidades", "Secundaria", 10 + i, 15 + i, 3, "ACTIVE", new[] { "tag" });
            await repo.AddAsync(cat, CancellationToken.None);
        }
        await ctx.SaveChangesAsync();

        var specPage1 = new CategoryFilterSpecification(ageMin: 12, ageMax: 14, page: 1, pageSize: 2);
        var p1 = await repo.ListAsync(specPage1, CancellationToken.None);
        var specPage2 = new CategoryFilterSpecification(ageMin: 12, ageMax: 14, page: 2, pageSize: 2);
        var p2 = await repo.ListAsync(specPage2, CancellationToken.None);

        // Both pages should respect filter + pagination (Skip/Take)
        Assert.True(specPage1.AsNoTracking);
        Assert.Equal(0, specPage1.Skip);
        Assert.Equal(2, specPage1.Take);
        Assert.Equal(2, specPage2.Skip);
        // p1 and p2 should not overlap
        var ids1 = p1.Select(c => c.Id.Value).ToHashSet();
        var ids2 = p2.Select(c => c.Id.Value).ToHashSet();
        Assert.Empty(ids1.Intersect(ids2));
    }

    [Fact]
    public void CategoryByIdSpecification_IsSatisfiedAndEF()
    {
        var cat = Make("C1", "Humanidades", "Secundaria", 13, 17, 3, "DRAFT", new[] { "historia" });
        var spec = new CategoryByIdSpecification(cat.Id);
        Assert.True(spec.IsSatisfiedBy(cat));
        var otherId = new CategoryId(Guid.NewGuid());
        var specOther = new CategoryByIdSpecification(otherId);
        Assert.False(specOther.IsSatisfiedBy(cat));
        Assert.True(spec.AsNoTracking);
    }

    [Fact]
    public async Task CategoryFilter_AsNoTracking_IsTrue()
    {
        var spec = new CategoryFilterSpecification(knowledgeArea: "Humanidades", paginate: false);
        Assert.True(spec.AsNoTracking);
        var specPaged = new CategoryFilterSpecification(page: 1, pageSize: 10);
        Assert.True(specPaged.AsNoTracking);
    }

    [Fact]
    public void Pagination_Skip_Take_Calculated()
    {
        var s1 = new CategoryFilterSpecification(page: 1, pageSize: 20);
        Assert.Equal(0, s1.Skip);
        Assert.Equal(20, s1.Take);
        var s2 = new CategoryFilterSpecification(page: 3, pageSize: 10);
        Assert.Equal(20, s2.Skip);
        Assert.Equal(10, s2.Take);
    }
}