using BuildingBlocks.Kernel.Domain.ValueObjects;

namespace OroQuizClash.Domain.Questions;

public sealed record AnswerOptionId(Guid Value) : StronglyTypedId<Guid>(Value)
{
    public static AnswerOptionId New() => new(Guid.NewGuid());
}
