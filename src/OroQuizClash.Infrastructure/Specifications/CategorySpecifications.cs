using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Games;

// Stub for category validation - in real SPEC-002, Category would be its own aggregate.
// Here we provide placeholder specs for Game queries by Category.
namespace OroQuizClash.Infrastructure.Specifications;

public sealed class GamesByCategorySpecification : Specification<Game>
{
    public GamesByCategorySpecification(Guid categoryId)
    {
        Where(g => g.Configuration.CategoryId.Value == categoryId);
    }
}