using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Games;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class PointTransactionTypeConfiguration : IEntityTypeConfiguration<PointTransaction>
{
    public void Configure(EntityTypeBuilder<PointTransaction> builder)
    {
        builder.ToTable("PointTransactions");
        builder.HasKey(pt => pt.Id);
        builder.Property(pt => pt.Id).HasConversion(id => id.Value, v => new PointTransactionId(v)).IsRequired();
        builder.Property(pt => pt.GameId).HasConversion(id => id.Value, v => new GameId(v)).IsRequired();
        builder.Property(pt => pt.PlayerId).IsRequired();
        builder.Property(pt => pt.RoundId).HasConversion(id => id.Value, v => new GameRoundId(v)).IsRequired();
        builder.Property(pt => pt.QuestionId).HasConversion(id => id.Value, v => new Domain.Questions.QuestionId(v)).IsRequired();
        builder.Property(pt => pt.AnswerId).HasConversion(id => id.Value, v => new AnswerId(v)).IsRequired();
        builder.Property(pt => pt.Type).HasConversion(t => t.Id, id => Domain.Games.Enumerations.PointTransactionType.FromId(id)).HasColumnName("TypeId").IsRequired();
        builder.Property(pt => pt.Points).IsRequired();
        builder.Property(pt => pt.CreatedAt).IsRequired();

        builder.HasIndex(pt => new { pt.GameId, pt.AnswerId }).IsUnique();
        builder.HasIndex(pt => new { pt.GameId, pt.PlayerId });
        builder.HasIndex(pt => new { pt.GameId, pt.RoundId });
    }
}
