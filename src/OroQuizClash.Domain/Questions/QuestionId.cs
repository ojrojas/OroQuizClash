using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Questions;

public sealed record QuestionId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static QuestionId New() => new(Guid.NewGuid());
}
