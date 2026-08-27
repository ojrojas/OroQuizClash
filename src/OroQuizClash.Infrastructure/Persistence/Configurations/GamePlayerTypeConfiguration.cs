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
        builder.HasIndex(p => new { p.GameId, p.UserId }).IsUnique();
        builder.HasIndex(p => p.GameId);
    }
}
