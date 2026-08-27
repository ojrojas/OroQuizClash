using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Games;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class GamePlayerTypeConfiguration : IEntityTypeConfiguration<GamePlayer>
{
    public void Configure(EntityTypeBuilder<GamePlayer> builder)
    {
        builder.ToTable("GamePlayers");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasConversion(id => id.Value, v => new GamePlayerId(v)).IsRequired();
        builder.Property(p => p.GameId).HasConversion(id => id.Value, v => new GameId(v)).IsRequired();
        builder.Property(p => p.UserId).IsRequired();
        builder.Property(p => p.JoinedAt).IsRequired();
        builder.Property(p => p.DisplayName).HasMaxLength(100);
        builder.Property(p => p.IsWithdrawn).IsRequired();
        builder.Property(p => p.WithdrawnAt);

        builder.OwnsOne(p => p.Score, sb =>
        {
            sb.Property(s => s.CurrentPoints).HasColumnName("CurrentPoints").IsRequired();
            sb.Property(s => s.SecuredPoints).HasColumnName("SecuredPoints").IsRequired();
            sb.Property(s => s.RoundPoints).HasColumnName("RoundPoints").IsRequired();
            sb.Property(s => s.PotentialPoints).HasColumnName("PotentialPoints").IsRequired();
            sb.Property(s => s.TotalPoints).HasColumnName("TotalPoints").IsRequired();
        });
        builder.Navigation(p => p.Score).IsRequired();

        builder.HasIndex(p => new { p.GameId, p.UserId }).IsUnique();
        builder.HasIndex(p => p.GameId);
    }
}
