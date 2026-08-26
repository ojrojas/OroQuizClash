using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Categories;

public sealed record CategoryId(Guid Value) : StronglyTypedId<Guid>(Value);