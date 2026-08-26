using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Categories.ValueObjects;

public sealed class CategoryTags : ValueObject
{
    public IReadOnlySet<string> Tags { get; }

    public IReadOnlySet<string> Value => Tags;

    public CategoryTags(IEnumerable<string> tags)
    {
        if (tags is null)
        {
            throw new ArgumentNullException(nameof(tags));
        }

        var normalized = tags
            .Where(t => t is not null)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalized.Count > 10)
        {
            throw new ArgumentException("Category cannot have more than 10 tags.", nameof(tags));
        }

        foreach (var tag in normalized)
        {
            if (tag.Length < 2 || tag.Length > 30)
            {
                throw new ArgumentException($"Each tag must be 2-30 characters. Invalid tag: '{tag}'.", nameof(tags));
            }
        }

        Tags = new HashSet<string>(normalized, StringComparer.Ordinal);
    }

    private CategoryTags()
    {
        Tags = new HashSet<string>(StringComparer.Ordinal);
    }

    public static CategoryTags Empty => new([]);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        // Sorted for deterministic equality
        var sorted = Tags.OrderBy(t => t, StringComparer.Ordinal).ToList();
        foreach (var tag in sorted)
        {
            yield return tag;
        }
    }
}