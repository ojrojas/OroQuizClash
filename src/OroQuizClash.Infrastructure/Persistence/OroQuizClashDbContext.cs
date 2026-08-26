using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Infrastructure.Outbox;
using BuildingBlocks.Kernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using OroQuizClash.Domain.Games;

namespace OroQuizClash.Infrastructure.Persistence;

public sealed class OroQuizClashDbContext(DbContextOptions<OroQuizClashDbContext> options, IDomainEventDispatcher dispatcher)
    : AppDbContextBase(options, dispatcher)
{
    public DbSet<Game> Games => Set<Game>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OroQuizClashDbContext).Assembly);
        modelBuilder.ApplyConfiguration(new OutboxEntityTypeConfiguration());
        base.OnModelCreating(modelBuilder);
    }
}