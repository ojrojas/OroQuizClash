using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Games;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class AnswerTypeConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("Answers");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasConversion(id => id.Value, v => new AnswerId(v)).IsRequired();
        builder.Property(a => a.GameId).HasConversion(id => id.Value, v => new GameId(v)).IsRequired();
        builder.Property(a => a.PlayerId).IsRequired();
        builder.Property(a => a.RoundId).HasConversion(id => id.Value, v => new GameRoundId(v)).IsRequired();
        builder.Property(a => a.QuestionId).HasConversion(id => id.Value, v => new Domain.Questions.QuestionId(v)).IsRequired();
        builder.Property(a => a.AnswerOptionId).HasConversion(id => id.Value, v => new Domain.Questions.AnswerOptionId(v)).IsRequired();
        builder.Property(a => a.Status).HasConversion(s => s.Id, id => Domain.Games.Enumerations.AnswerStatus.FromId(id)).HasColumnName("StatusId").IsRequired();
        builder.Property(a => a.Correct).IsRequired(false);
        builder.Property(a => a.Points).IsRequired();
        builder.Property(a => a.ElapsedTime).IsRequired();
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.Property(a => a.EvaluatedAt).IsRequired(false);
        builder.Property(a => a.RowVersion).IsRowVersion().IsConcurrencyToken().IsRequired();

        builder.HasIndex(a => new { a.GameId, a.PlayerId, a.RoundId }).IsUnique();
        builder.HasIndex(a => new { a.GameId, a.RoundId });
        builder.HasIndex(a => new { a.GameId, a.PlayerId });
        builder.HasIndex(a => a.Status);
    }
}
