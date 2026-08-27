using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class RewardTypeConfiguration : IEntityTypeConfiguration<Reward>
{
    public void Configure(EntityTypeBuilder<Reward> builder)
    {
        builder.ToTable("Rewards");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => new RewardId(v)).IsRequired();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(500).IsRequired();
        builder.Property(r => r.PointsRequired).IsRequired();
        builder.Property(r => r.Stock).IsRequired();
        builder.Property(r => r.Status)
            .HasConversion(s => s.Id, id => RewardStatus.FromId(id))
            .HasColumnName("Status")
            .IsRequired();
        builder.Property(r => r.ExpirationDate);
        builder.Property(r => r.CreatedAt).IsRequired();
        builder.Property(r => r.UpdatedAt);
        builder.Property(r => r.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.HasIndex(r => r.Status);
    }
}
