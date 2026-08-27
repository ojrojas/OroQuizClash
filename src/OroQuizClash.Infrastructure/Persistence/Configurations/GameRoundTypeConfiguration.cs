using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Questions;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class GameRoundTypeConfiguration : IEntityTypeConfiguration<GameRound>
{
    public void Configure(EntityTypeBuilder<GameRound> builder)
    {
        builder.ToTable("GameRounds");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasConversion(id => id.Value, v => new GameRoundId(v)).IsRequired();
        builder.Property(r => r.GameId).HasConversion(id => id.Value, v => new GameId(v)).IsRequired();
        builder.Property(r => r.RoundNumber).IsRequired();
        builder.Property(r => r.QuestionId).HasConversion(id => id.Value, v => new QuestionId(v)).IsRequired();
        builder.Property(r => r.Status).HasConversion(s => s.Id, id => Domain.Games.Enumerations.GameStatus.FromId(id)).IsRequired();
        builder.Property(r => r.StartedAt).IsRequired();
        builder.Property(r => r.CompletedAt);
        builder.HasIndex(r => new { r.GameId, r.RoundNumber }).IsUnique();
        builder.HasIndex(r => r.QuestionId);
        builder.HasIndex(r => r.GameId);
    }
}
