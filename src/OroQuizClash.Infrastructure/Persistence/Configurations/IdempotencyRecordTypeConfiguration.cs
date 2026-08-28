using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Audit;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class IdempotencyRecordTypeConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecords");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).IsRequired();
        builder.Property(r => r.Key).HasMaxLength(200).IsRequired();
        builder.Property(r => r.ActorId).HasMaxLength(100).IsRequired();
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.ResponseHash).HasMaxLength(500).IsRequired();
        builder.Property(r => r.Response).HasMaxLength(4000).IsRequired();

        builder.HasIndex(r => new { r.Key, r.ActorId }).IsUnique();
        builder.HasIndex(r => r.CreatedAt);
    }
}
