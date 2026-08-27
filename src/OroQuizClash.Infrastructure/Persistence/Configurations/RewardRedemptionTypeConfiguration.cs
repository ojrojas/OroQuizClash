using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Rewards;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class RewardRedemptionTypeConfiguration : IEntityTypeConfiguration<RewardRedemption>
{
    public void Configure(EntityTypeBuilder<RewardRedemption> builder)
    {
        builder.ToTable("RewardRedemptions");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => new RewardRedemptionId(v)).IsRequired();
        builder.Property(r => r.RewardId).HasConversion(id => id.Value, v => new RewardId(v)).IsRequired();
        builder.Property(r => r.GameId).HasConversion(id => id.Value, v => new GameId(v)).IsRequired();
        builder.Property(r => r.PlayerId).IsRequired();
        builder.Property(r => r.Points).IsRequired();
        builder.Property(r => r.Status)
            .HasConversion(s => s.Id, id => RedemptionStatus.FromId(id))
            .HasColumnName("Status")
            .IsRequired();
        builder.Property(r => r.RequestedAt).IsRequired();
        builder.Property(r => r.DeliveredAt);
        builder.Property(r => r.IdempotencyKey);
        builder.Property(r => r.RowVersion).IsRowVersion().IsConcurrencyToken();

        builder.HasIndex(r => r.PlayerId);
        builder.HasIndex(r => r.RewardId);
        builder.HasIndex(r => r.Status);
        builder.HasIndex(r => new { r.PlayerId, r.IdempotencyKey })
            .IsUnique()
            .HasFilter("[IdempotencyKey] IS NOT NULL");

        builder.OwnsMany(r => r.Transitions, tb =>
        {
            tb.ToTable("RedemptionTransitions");
            tb.HasKey(t => t.Id);
            tb.Property(t => t.Id).HasConversion(id => id.Value, v => new RedemptionTransitionId(v)).IsRequired();
            tb.Property(t => t.Status)
                .HasConversion(s => s.Id, id => RedemptionStatus.FromId(id))
                .HasColumnName("Status")
                .IsRequired();
            tb.Property(t => t.ActorId).IsRequired();
            tb.Property(t => t.At).IsRequired();
        });
    }
}
