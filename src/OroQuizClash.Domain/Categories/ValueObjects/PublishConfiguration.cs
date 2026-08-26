using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Categories.ValueObjects;

public sealed class PublishConfiguration : ValueObject
{
    public bool RequiresModeration { get; }

    public PublishConfiguration(bool requiresModeration)
    {
        RequiresModeration = requiresModeration;
    }

    private PublishConfiguration()
    {
        RequiresModeration = false;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RequiresModeration;
    }
}