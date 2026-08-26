using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Categories.ValueObjects;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

public sealed class CategoryTypeConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasConversion(id => id.Value, v => new CategoryId(v))
            .IsRequired();

        builder.Property(c => c.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Description)
            .HasMaxLength(500);

        builder.Property(c => c.KnowledgeArea)
            .HasConversion(ka => ka.Value, v => new KnowledgeArea(v))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.AcademicLevel)
            .HasConversion(al => al.Value, v => new AcademicLevel(v))
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.DifficultyLevel)
            .HasConversion(d => d.Value, v => new DifficultyLevel(v))
            .IsRequired();

        builder.Property(c => c.Status)
            .HasConversion(s => s.Id, id => CategoryStatus.FromId(id))
            .IsRequired();

        builder.Property(c => c.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        builder.Property(c => c.CreatedAt)
            .IsRequired();

        builder.Property(c => c.CreatedBy)
            .IsRequired();

        builder.Property(c => c.Tags)
            .HasConversion(
                tags => string.Join(",", tags.Tags),
                csv => string.IsNullOrWhiteSpace(csv)
                    ? CategoryTags.Empty
                    : new CategoryTags(csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))
            .HasMaxLength(1000);

        builder.OwnsOne(c => c.AgeRange, ab =>
        {
            ab.Property(a => a.Min).HasColumnName("AgeMin").IsRequired();
            ab.Property(a => a.Max).HasColumnName("AgeMax").IsRequired();
            ab.WithOwner();
        });

        builder.OwnsOne(c => c.PublishConfiguration, pb =>
        {
            pb.Property(p => p.RequiresModeration)
                .HasColumnName("PublishConfiguration_RequiresModeration")
                .IsRequired();
            pb.WithOwner();
        });

        builder.HasIndex(c => c.Status);
        builder.HasIndex(c => c.KnowledgeArea);
        builder.HasIndex(c => c.AcademicLevel);
        builder.HasIndex(c => new { c.KnowledgeArea, c.AcademicLevel });
    }
}