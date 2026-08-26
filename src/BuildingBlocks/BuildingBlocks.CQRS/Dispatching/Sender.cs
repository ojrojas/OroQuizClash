namespace BuildingBlocks.CQRS.Dispatching;

/// <summary>
/// Reflection-free-at-call-time sender: builds one wrapper per request type,
/// caches it, and from then on dispatching is a dictionary lookup plus virtual calls.
/// </summary>
public sealed class Sender(IServiceProvider serviceProvider) : ISender
{
    private static readonly ConcurrentDictionary<Type, RequestHandlerWrapperBase> Wrappers = new();

    public Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (RequestHandlerWrapper<TResponse>)Wrappers.GetOrAdd(request.GetType(), static (requestType, responseType) =>
        {
            var wrapperType = typeof(RequestHandlerWrapperImpl<,>).MakeGenericType(requestType, responseType);
            return (RequestHandlerWrapperBase)Activator.CreateInstance(wrapperType)!;
        }, typeof(TResponse));

        return wrapper.HandleAsync(request, serviceProvider, cancellationToken);
    }

    private abstract class RequestHandlerWrapperBase;

    private abstract class RequestHandlerWrapper<TResponse> : RequestHandlerWrapperBase
    {
        public abstract Task<TResponse> HandleAsync(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken);
    }

    private sealed class RequestHandlerWrapperImpl<TRequest, TResponse> : RequestHandlerWrapper<TResponse>
        where TRequest : IRequest<TResponse>
    {
        public override Task<TResponse> HandleAsync(
            IRequest<TResponse> request,
            IServiceProvider serviceProvider,
            CancellationToken cancellationToken)
        {
            var handler = serviceProvider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

            RequestHandlerDelegate<TResponse> pipeline = ct => handler.HandleAsync((TRequest)request, ct);

            // Wrap behaviors outside-in so the first registered behavior runs first.
            var behaviors = serviceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>().Reverse();

            foreach (var behavior in behaviors)
            {
                var next = pipeline;
                pipeline = ct => behavior.HandleAsync((TRequest)request, next, ct);
            }

            return pipeline(cancellationToken);
        }
    }
}