using System.Text.Json;

using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.AspNetCore.Http;

using OroQuizClash.Infrastructure.Services;

namespace OroQuizClash.Application.Behaviors;

public sealed class IdempotencyBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor,
    IIdempotencyService idempotencyService) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var key = httpContext?.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(key))
        {
            return await next(cancellationToken);
        }

        var actorId = httpContext?.User?.FindFirst("sub")?.Value ?? "anonymous";
        var requestHash = IdempotencyService.ComputeHash(request!);

        var (isDuplicate, cachedResponse, isReplay) = await idempotencyService.CheckAsync(key, actorId, requestHash, cancellationToken);

        if (isReplay)
        {
            return CreateFailure<TResponse>("Idempotency.ReplayDetected", "Idempotency key reused with different payload.");
        }

        if (isDuplicate && cachedResponse is not null)
        {
            try
            {
                var cached = JsonSerializer.Deserialize<TResponse>(cachedResponse);
                if (cached is not null) return cached;
            }
            catch
            {
                // fallback to handler if deserialization fails
            }
        }

        var response = await next(cancellationToken);

        if (response is Result result && result.IsSuccess)
        {
            var json = JsonSerializer.Serialize(response);
            await idempotencyService.StoreAsync(key, actorId, requestHash, json, cancellationToken);
        }
        else if (response is not null)
        {
            var json = JsonSerializer.Serialize(response);
            await idempotencyService.StoreAsync(key, actorId, requestHash, json, cancellationToken);
        }

        return response;
    }

    private static TResponse CreateFailure<TResponse>(string code, string description)
    {
        var error = Error.Validation(code, description);
        var responseType = typeof(TResponse);
        if (responseType == typeof(Result))
        {
            var result = Result.Failure(error);
            return (TResponse)(object)result;
        }
        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var method = typeof(Result).GetMethod(nameof(Result.Failure), 1, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, null, [typeof(Error)], null);
            if (method is not null)
            {
                var generic = method.MakeGenericMethod(valueType);
                var result = generic.Invoke(null, [error]);
                return (TResponse)result!;
            }
        }
        throw new InvalidOperationException($"Cannot create failure for {responseType.Name}");
    }
}
