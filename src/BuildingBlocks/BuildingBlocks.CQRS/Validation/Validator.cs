namespace BuildingBlocks.CQRS.Validation;

/// <summary>
/// Synchronous rule-based validator base:
/// <code>
/// public sealed class CreateOrderValidator : Validator&lt;CreateOrderCommand&gt;
/// {
///     public CreateOrderValidator()
///     {
///         RuleFor(x => x.CustomerId != Guid.Empty, nameof(CreateOrderCommand.CustomerId), "CustomerId is required.");
///     }
/// }
/// </code>
/// </summary>
public abstract class Validator<TRequest> : IValidator<TRequest>
{
    private readonly List<(Func<TRequest, bool> IsValid, string PropertyName, string ErrorMessage)> _rules = [];

    protected void RuleFor(Func<TRequest, bool> isValid, string propertyName, string errorMessage) =>
        _rules.Add((isValid, propertyName, errorMessage));

    public Task<IReadOnlyCollection<ValidationFailure>> ValidateAsync(TRequest request, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<ValidationFailure> failures =
        [
            .. _rules
                .Where(rule => !rule.IsValid(request))
                .Select(rule => new ValidationFailure(rule.PropertyName, rule.ErrorMessage))
        ];

        return Task.FromResult(failures);
    }
}
