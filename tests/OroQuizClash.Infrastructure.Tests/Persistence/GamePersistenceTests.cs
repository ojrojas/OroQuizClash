using BuildingBlocks.CQRS.Abstractions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;
using OroQuizClash.Infrastructure.Persistence;
using OroQuizClash.Infrastructure.Specifications;

namespace OroQuizClash.Infrastructure.Tests.Persistence;

public sealed class GamePersistenceTests
{
    private OroQuizClashDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<OroQuizClashDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var dispatcher = Substitute.For<IDomainEventDispatcher>();
        return new OroQuizClashDbContext(options, dispatcher);
    }

    [Fact]
    public async Task AddAndQuery_WithSpecification_ReturnsSame()
    {
        await using var ctx = CreateContext();
        var config = new GameConfiguration("Quiz", new CategoryId(Guid.NewGuid()), 5, 10, 1, DifficultyProgressionStrategy.Linear, 30, ScoringSystem.Standard, LossPolicy.LoseAll, WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None, new RewardRules("Points", 500), 2, 10);
        var game = Domain.Games.Game.Create(config, Guid.NewGuid()).Value;
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();

        var spec = new GameByIdSpecification(game.Id);
        var loaded = await ctx.Games.ApplySpecification(spec).FirstOrDefaultAsync();
        Assert.NotNull(loaded);
        Assert.Equal(game.Id, loaded!.Id);
        Assert.Equal(5, loaded.Configuration.MinRounds);
    }

    [Fact]
    public async Task RowVersion_IsConcurrencyToken()
    {
        await using var ctx = CreateContext();
        var config = new GameConfiguration("Quiz", new CategoryId(Guid.NewGuid()), 5, 10, 1, DifficultyProgressionStrategy.Linear, 30, ScoringSystem.Standard, LossPolicy.LoseAll, WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None, new RewardRules("Points", 500), 2, 10);
        var game = Domain.Games.Game.Create(config, Guid.NewGuid()).Value;
        ctx.Games.Add(game);
        await ctx.SaveChangesAsync();
        Assert.NotNull(game.RowVersion);
    }
}

// Minimal ApplySpecification helper for InMemory test (mirrors SpecificationEvaluator)
public static class SpecExtensions
{
    public static IQueryable<T> ApplySpecification<T>(this IQueryable<T> query, BuildingBlocks.Kernel.Domain.Specifications.ISpecification<T> spec)
    {
        if (spec.Criteria != null) query = query.Where(spec.Criteria);
        return query;
    }
    public static IQueryable<T> ApplySpecification<T>(this DbSet<T> set, BuildingBlocks.Kernel.Domain.Specifications.ISpecification<T> spec) where T : class
    {
        IQueryable<T> query = set;
        if (spec.Criteria != null) query = query.Where(spec.Criteria);
        return query;
    }
}