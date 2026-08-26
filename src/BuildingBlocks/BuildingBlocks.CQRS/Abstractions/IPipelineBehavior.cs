
namespace BuildingBlocks.CQRS.Abstractions;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken);

/// <summary>
/// Cross-cutting middleware around request handling (logging, validation, transactions...).
/// Behaviors run in registration order; the innermost call is the handler itself.
/// </summary>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken);
}