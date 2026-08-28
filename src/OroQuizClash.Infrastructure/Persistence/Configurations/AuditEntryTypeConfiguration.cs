using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Audit;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class AuditEntryTypeConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        builder.ToTable("AuditEntries");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).IsRequired();
        builder.Property(e => e.Timestamp).IsRequired();
        builder.Property(e => e.ActorId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.ActorRoles).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Action).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Permission).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Resource).HasMaxLength(200).IsRequired();
        builder.Property(e => e.CorrelationId).HasMaxLength(100).IsRequired();
        builder.Property(e => e.TenantId).HasMaxLength(100);
        builder.Property(e => e.Result).HasMaxLength(50).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(200);
        builder.Property(e => e.Details).HasMaxLength(1000);

        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.Resource);
        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.ActorId);
    }
}
