namespace BuildingBlocks.CQRS.Validation;

public sealed class ValidationException(IReadOnlyCollection<ValidationFailure> failures)
    : Exception("One or more validation failures occurred.")
{
    public IReadOnlyCollection<ValidationFailure> Failures { get; } = failures;
}
