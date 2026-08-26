namespace BuildingBlocks.CQRS.Abstractions;

/// <summary>
/// Entry point of the application layer: routes a command or query
/// to its single handler, running the configured pipeline behaviors.
/// </summary>
public interface ISender
{
    Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}