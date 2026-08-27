using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Questions;
using OroQuizClash.Domain.Questions.ValueObjects;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class QuestionTypeConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id)
            .HasConversion(id => id.Value, v => new QuestionId(v))
            .IsRequired();

        builder.Property(q => q.Text)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(q => q.CategoryId)
            .HasConversion(id => id.Value, v => new CategoryId(v))
            .HasColumnName("CategoryId")
            .IsRequired();

        builder.Property(q => q.Difficulty)
            .HasConversion(d => d.Id, id => DifficultyLevel.FromId(id))
            .HasColumnName("DifficultyId")
            .IsRequired();

        builder.Property(q => q.AcademicLevel)
            .HasConversion(al => al.Value, v => new AcademicLevel(v))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(q => q.Status)
            .HasConversion(s => s.Id, id => QuestionStatus.FromId(id))
            .IsRequired();

        builder.Property(q => q.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Property(q => q.CreatedAt)
            .IsRequired();

        builder.Property(q => q.UpdatedAt)
            .IsRequired();

        builder.Property(q => q.PublishedAt);

        builder.Property(q => q.CreatedBy)
            .IsRequired();

        builder.OwnsOne(q => q.AgeRange, ab =>
        {
            ab.Property(a => a.Min).HasColumnName("AgeMin").IsRequired();
            ab.Property(a => a.Max).HasColumnName("AgeMax").IsRequired();
            ab.WithOwner();
        });

        // AnswerOptions as owned collection via HasMany
        builder.HasMany(q => q.AnswerOptions)
            .WithOne()
            .HasForeignKey("QuestionId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(q => new { q.CategoryId, q.Status });
        builder.HasIndex(q => q.Difficulty);
        builder.HasIndex(q => q.Status);
        builder.HasIndex(q => new { q.CategoryId, q.Status, q.Difficulty });
        builder.HasIndex(q => q.AcademicLevel);
    }
}

public sealed class AnswerOptionTypeConfiguration : IEntityTypeConfiguration<AnswerOption>
{
    public void Configure(EntityTypeBuilder<AnswerOption> builder)
    {
        builder.ToTable("AnswerOptions");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, v => new AnswerOptionId(v))
            .IsRequired();

        builder.Property(a => a.QuestionId)
            .HasConversion(id => id.Value, v => new QuestionId(v))
            .HasColumnName("QuestionId")
            .IsRequired();

        builder.Property(a => a.Text)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(a => a.IsCorrect)
            .IsRequired();

        builder.Property(a => a.DisplayOrder)
            .IsRequired();

        builder.HasIndex(a => a.QuestionId);
        builder.HasIndex(a => new { a.QuestionId, a.DisplayOrder }).IsUnique();
        builder.HasIndex(a => new { a.QuestionId, a.IsCorrect });
    }
}
