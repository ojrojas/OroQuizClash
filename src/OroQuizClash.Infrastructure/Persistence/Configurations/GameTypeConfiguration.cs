using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Games;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class GameTypeConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable("Games");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasConversion(id => id.Value, v => new GameId(v)).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(100).IsRequired();
        builder.Property(g => g.Status).HasConversion(s => s.Id, id => Domain.Games.Enumerations.GameStatus.FromId(id)).IsRequired();
        builder.Property(g => g.RowVersion).IsRowVersion().IsConcurrencyToken();
        builder.Property(g => g.CreatedAt).IsRequired();
        builder.Property(g => g.ReadyAt);
        builder.Property(g => g.StartedAt);
        builder.Property(g => g.FinishedAt);
        builder.Property(g => g.CreatedBy).IsRequired();

        builder.HasMany(g => g.Players)
            .WithOne()
            .HasForeignKey("GameId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Players).HasField("_players").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(g => g.Rounds)
            .WithOne()
            .HasForeignKey("GameId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Rounds).HasField("_rounds").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(g => g.Answers)
            .WithOne()
            .HasForeignKey("GameId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.Answers).HasField("_answers").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(g => g.PointTransactions)
            .WithOne()
            .HasForeignKey("GameId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(g => g.PointTransactions).HasField("_pointTransactions").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsOne(g => g.Configuration, cb =>
        {
            cb.Property(c => c.Name).HasColumnName("Configuration_Name").HasMaxLength(100);
            cb.Property(c => c.CategoryId).HasConversion(id => id.Value, v => new Domain.Categories.CategoryId(v)).HasColumnName("CategoryId");
            cb.Property(c => c.MinRounds).HasColumnName("MinRounds");
            cb.Property(c => c.MaxRounds).HasColumnName("MaxRounds");
            cb.Property(c => c.InitialDifficulty).HasColumnName("InitialDifficulty");
            cb.Property(c => c.DifficultyStrategy).HasConversion(s => s.Id, id => Domain.Games.Enumerations.DifficultyProgressionStrategy.FromId(id)).HasColumnName("DifficultyStrategy");
            cb.Property(c => c.TimeLimitPerQuestionSeconds).HasColumnName("TimeLimitSeconds");
            cb.Property(c => c.ScoringSystem).HasConversion(s => s.Id, id => Domain.Games.Enumerations.ScoringSystem.FromId(id)).HasColumnName("ScoringSystem");
            cb.Property(c => c.LossPolicy).HasConversion(s => s.Id, id => Domain.Games.Enumerations.LossPolicy.FromId(id)).HasColumnName("LossPolicy");
            cb.Property(c => c.WithdrawalPolicy).HasConversion(s => s.Id, id => Domain.Games.Enumerations.WithdrawalPolicy.FromId(id)).HasColumnName("WithdrawalPolicy");
            cb.Property(c => c.ConsolationPolicy).HasConversion(s => s.Id, id => Domain.Games.Enumerations.ConsolationPolicy.FromId(id)).HasColumnName("ConsolationPolicy");
            cb.Property(c => c.MinPlayers).HasColumnName("MinPlayers");
            cb.Property(c => c.MaxPlayers).HasColumnName("MaxPlayers");
            cb.Property(c => c.PointsPerRound).HasColumnName("PointsPerRound");
            cb.OwnsOne(c => c.RewardRules, rb =>
            {
                rb.Property(r => r.Type).HasColumnName("RewardRules_Type").HasMaxLength(50);
                rb.Property(r => r.Threshold).HasColumnName("RewardRules_Threshold");
            });
        });

        builder.HasIndex(g => g.Status);
        builder.HasIndex(g => g.CreatedAt);
    }
}