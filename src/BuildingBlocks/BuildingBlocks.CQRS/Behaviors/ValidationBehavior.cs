namespace BuildingBlocks.CQRS.Behaviors;

/// <summary>
/// Runs all registered <see cref="IValidator{TRequest}"/> before the handler and
/// throws <see cref="ValidationException"/> if any rule fails.
/// </summary>
public sealed class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();

        foreach (var validator in validators)
        {
            failures.AddRange(await validator.ValidateAsync(request, cancellationToken).ConfigureAwait(false));
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken).ConfigureAwait(false);
    }
}
