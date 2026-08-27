using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Infrastructure.Outbox;
using BuildingBlocks.Kernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Infrastructure.Persistence;

public sealed class OroQuizClashDbContext(DbContextOptions<OroQuizClashDbContext> options, IDomainEventDispatcher dispatcher)
    : AppDbContextBase(options, dispatcher)
{
    public DbSet<Game> Games => Set<Game>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Question> Questions => Set<Question>();

    public DbSet<Reward> Rewards => Set<Reward>();

    public DbSet<RewardRedemption> RewardRedemptions => Set<RewardRedemption>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OroQuizClashDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxEntityTypeConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}