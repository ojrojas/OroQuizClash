using System.Reflection;

using BuildingBlocks.CQRS.Abstractions;
using BuildingBlocks.Kernel.Domain.Results;

using Microsoft.AspNetCore.Http;

using OroQuizClash.Domain.Authorization;

namespace OroQuizClash.Application.Behaviors;

public sealed class AuthorizationBehavior<TRequest, TResponse>(
    IHttpContextAccessor httpContextAccessor) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> HandleAsync(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var permissionAttr = typeof(TRequest).GetCustomAttribute<Authorization.RequiresPermissionAttribute>(inherit: true);
        if (permissionAttr is null)
        {
            return await next(cancellationToken);
        }

        var httpContext = httpContextAccessor.HttpContext;
        var user = httpContext?.User;

        if (user is null || user.Identity?.IsAuthenticated != true)
        {
            return CreateForbidden<TResponse>("Auth.Unauthenticated", "Authentication required.");
        }

        var requiredPermission = permissionAttr.Permission;
        var userRoles = GetUserRoles(user);

        var hasPermission = userRoles.Any(role => role.HasPermission(requiredPermission));
        if (!hasPermission)
        {
            return CreateForbidden<TResponse>("Auth.Forbidden", $"Missing required permission: {requiredPermission.Name}");
        }

        return await next(cancellationToken);
    }

    private static List<Role> GetUserRoles(System.Security.Claims.ClaimsPrincipal user)
    {
        var roles = new List<Role>();
        var roleValues = user.FindAll("roles").Select(c => c.Value)
            .Concat(user.FindAll("role").Select(c => c.Value))
            .Concat(user.FindAll(System.Security.Claims.ClaimTypes.Role).Select(c => c.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var rv in roleValues)
        {
            var role = Role.All.FirstOrDefault(r => r.Name.Equals(rv, StringComparison.OrdinalIgnoreCase));
            if (role is not null) roles.Add(role);
        }

        return roles;
    }

    private static TResponse CreateForbidden<TResponse>(string code, string description)
    {
        var error = Error.Forbidden(code, description);
        var responseType = typeof(TResponse);

        if (responseType == typeof(Result))
        {
            var result = Result.Failure(error);
            return (TResponse)(object)result;
        }

        if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
        {
            var valueType = responseType.GetGenericArguments()[0];
            var method = typeof(Result).GetMethod(nameof(Result.Failure), 1, BindingFlags.Public | BindingFlags.Static, null, [typeof(Error)], null);
            if (method is not null)
            {
                var generic = method.MakeGenericMethod(valueType);
                var result = generic.Invoke(null, [error]);
                return (TResponse)result!;
            }
        }

        throw new InvalidOperationException($"Cannot create forbidden result for response type {responseType.Name}. Ensure TResponse is Result or Result<T>.");
    }
}
