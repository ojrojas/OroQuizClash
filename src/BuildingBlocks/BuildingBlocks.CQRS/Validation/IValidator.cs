namespace BuildingBlocks.CQRS.Validation;

/// <summary>
/// Lightweight validation contract (no FluentValidation dependency).
/// Register implementations in DI; the ValidationBehavior runs them before the handler.
/// </summary>
public interface IValidator<in TRequest>
{
    Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(TRequest request, CancellationToken cancellationToken);
}

public sealed record ValidationFailure(string PropertyName, string ErrorMessage);