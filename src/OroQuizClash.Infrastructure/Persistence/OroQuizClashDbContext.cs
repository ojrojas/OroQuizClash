using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Infrastructure.Outbox;
using BuildingBlocks.Kernel.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

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

        // SQL Server generates rowversion server-side; SQLite cannot. On SQLite the
        // store-generated pattern is disabled so the client-stamped versions (see
        // BumpSqliteRowVersions) are persisted and optimistic concurrency still works.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            // SQLite cannot translate ORDER BY (and several comparisons) on
            // DateTimeOffset. Storing UTC DateTime keeps ordering/sorting fully
            // translatable while preserving the domain DateTimeOffset CLR type.
            var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, DateTime>(
                v => v.UtcDateTime,
                v => new DateTimeOffset(DateTime.SpecifyKind(v, DateTimeKind.Utc), TimeSpan.Zero));
            var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, DateTime?>(
                v => v.HasValue ? v.Value.UtcDateTime : null,
                v => v.HasValue ? new DateTimeOffset(DateTime.SpecifyKind(v.Value, DateTimeKind.Utc), TimeSpan.Zero) : null);

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.IsConcurrencyToken && property.ClrType == typeof(byte[]))
                        property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
                    else if (property.ClrType == typeof(DateTimeOffset))
                        property.SetValueConverter(dateTimeOffsetConverter);
                    else if (property.ClrType == typeof(DateTimeOffset?))
                        property.SetValueConverter(nullableDateTimeOffsetConverter);
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        BumpSqliteRowVersions();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        BumpSqliteRowVersions();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    // SQL Server generates rowversion values server-side; SQLite has no equivalent.
    // To keep optimistic-concurrency semantics identical on the SQLite fallback,
    // a fresh version is stamped client-side for every added/modified rowversion row.
    private void BumpSqliteRowVersions()
    {
        if (Database.ProviderName != "Microsoft.EntityFrameworkCore.Sqlite") return;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;

            foreach (var property in entry.Properties)
            {
                var metadata = property.Metadata;
                if (!metadata.IsConcurrencyToken || metadata.ClrType != typeof(byte[])) continue;
                property.CurrentValue = Guid.NewGuid().ToByteArray();
                property.IsModified = true;
            }
        }
    }
}