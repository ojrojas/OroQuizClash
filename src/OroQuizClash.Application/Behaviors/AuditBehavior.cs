using System.Diagnostics;
using System.Reflection;

using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using OroQuizClash.Domain.Audit;
using OroQuizClash.Infrastructure.Persistence;

namespace OroQuizClash.Application.Behaviors;

public sealed class AuditBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor,
    OroQuizClashDbContext dbContext,
    ILogger<AuditBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        try
        {
            var httpContext = httpContextAccessor.HttpContext;
            var user = httpContext?.User;
            var actorId = user?.FindFirst("sub")?.Value ?? user?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            var actorRoles = string.Join(",", user?.FindAll("roles").Select(c => c.Value).Concat(user?.FindAll("role").Select(c => c.Value) ?? []) ?? []);
            var correlationId = httpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                ?? Activity.Current?.Id
                ?? httpContext?.TraceIdentifier
                ?? Guid.NewGuid().ToString();

            var tenantId = user?.FindFirst("tenant_id")?.Value;
            var action = typeof(TRequest).Name;
            var permissionAttr = typeof(TRequest).GetCustomAttribute<Authorization.RequiresPermissionAttribute>(inherit: true);
            var permission = permissionAttr?.Permission.Name ?? "None";
            var resource = ExtractResource(request);
            var (result, reason, details) = ExtractResult(response);

            var entry = AuditEntry.Create(
                DateTimeOffset.UtcNow,
                actorId,
                actorRoles,
                action,
                permission,
                resource,
                correlationId,
                tenantId,
                result,
                reason,
                details);

            dbContext.AuditEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write audit entry for {Action}", typeof(TRequest).Name);
        }

        return response;
    }

    private static string ExtractResource(TRequest request)
    {
        var prop = request.GetType().GetProperty("GameId") ?? request.GetType().GetProperty("Id");
        if (prop?.GetValue(request) is Guid guid && guid != Guid.Empty) return $"{request.GetType().Name}:{guid}";
        if (prop?.GetValue(request) is not null) return $"{request.GetType().Name}:{prop.GetValue(request)}";
        return typeof(TRequest).Name;
    }

    private static (string result, string? reason, string? details) ExtractResult(TResponse response)
    {
        if (response is Result resultBase)
        {
            if (resultBase.IsSuccess) return ("Success", null, null);
            var error = resultBase.Error;
            var result = error.Type switch
            {
                ErrorType.Forbidden => "Denied",
                ErrorType.Unauthorized => "Denied",
                ErrorType.Validation => "ValidationFailed",
                ErrorType.Conflict => "Conflict",
                _ => "Failure"
            };
            return (result, error.Code, error.Description);
        }
        return ("Success", null, null);
    }
}
