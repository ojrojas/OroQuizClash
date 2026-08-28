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

        // SC-005: read queries must not generate audit
        var requestName = typeof(TRequest).Name;
        if (requestName.StartsWith("Get") || requestName.StartsWith("List") || requestName.StartsWith("Search"))
        {
            return response;
        }

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
            var rawAction = typeof(TRequest).Name;
            var action = MapToAuditAction(rawAction);
            var permissionAttr = typeof(TRequest).GetCustomAttribute<Authorization.RequiresPermissionAttribute>(inherit: true);
            var permission = permissionAttr?.Permission.Name ?? "None";
            var resource = ExtractResource(request);
            var resourceId = ExtractResourceId(request);
            var gameId = ExtractGameId(request);
            var playerId = ExtractPlayerId(request, actorId);
            var data = ExtractData(request, response);
            var (result, reason, details) = ExtractResult(response);
            // Data takes precedence over Details for new field, keep both for compat
            var entryData = data ?? details;

            var entry = AuditEntry.Create(
                DateTimeOffset.UtcNow,
                actorId,
                actorRoles,
                action,
                permission,
                resource,
                resourceId,
                gameId,
                playerId,
                correlationId,
                tenantId,
                result,
                reason,
                entryData);

            dbContext.AuditEntries.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to write audit entry for {Action}", typeof(TRequest).Name);
        }

        return response;
    }

    private static string MapToAuditAction(string rawAction)
    {
        var name = rawAction.Replace("Command", "").Replace("Query", "");
        if (name.Contains("CreateGame", StringComparison.OrdinalIgnoreCase)) return AuditAction.GameCreated.Name;
        if (name.Contains("ConfigureGame", StringComparison.OrdinalIgnoreCase) || name.Contains("UpdateGame", StringComparison.OrdinalIgnoreCase)) return AuditAction.GameConfigured.Name;
        if (name.Contains("StartGame", StringComparison.OrdinalIgnoreCase)) return AuditAction.GameStarted.Name;
        if (name.Contains("JoinGame", StringComparison.OrdinalIgnoreCase)) return AuditAction.PlayerJoined.Name;
        if (name.Contains("StartRound", StringComparison.OrdinalIgnoreCase)) return AuditAction.RoundStarted.Name;
        if (name.Contains("SubmitAnswer", StringComparison.OrdinalIgnoreCase)) return AuditAction.AnswerSubmitted.Name;
        if (name.Contains("EvaluateAnswer", StringComparison.OrdinalIgnoreCase)) return AuditAction.AnswerEvaluated.Name;
        if (name.Contains("Withdraw", StringComparison.OrdinalIgnoreCase)) return AuditAction.PlayerWithdrawn.Name;
        if (name.Contains("Eliminate", StringComparison.OrdinalIgnoreCase)) return AuditAction.PlayerEliminated.Name;
        if (name.Contains("FinishGame", StringComparison.OrdinalIgnoreCase)) return AuditAction.GameFinished.Name;
        if (name.Contains("Redeem", StringComparison.OrdinalIgnoreCase)) return AuditAction.RewardRedeemed.Name;
        if (name.Contains("Consolation", StringComparison.OrdinalIgnoreCase)) return AuditAction.ConsolationGranted.Name;
        if (name.Contains("Adjust", StringComparison.OrdinalIgnoreCase) || name.Contains("Administrative", StringComparison.OrdinalIgnoreCase)) return AuditAction.AdministrativeAdjustment.Name;
        if (AuditAction.All.Any(a => a.Name == name)) return name;
        if (AuditAction.All.Any(a => a.Name == rawAction)) return rawAction;
        return name;
    }

    private static string? ExtractResourceId(TRequest request)
    {
        var prop = request.GetType().GetProperty("Id") ?? request.GetType().GetProperty("GameId") ?? request.GetType().GetProperty("ResourceId");
        var val = prop?.GetValue(request);
        return val switch
        {
            Guid g when g != Guid.Empty => g.ToString(),
            string s when !string.IsNullOrWhiteSpace(s) => s,
            _ => val?.ToString()
        };
    }

    private static Guid? ExtractGameId(TRequest request)
    {
        var prop = request.GetType().GetProperty("GameId");
        if (prop?.GetValue(request) is Guid g && g != Guid.Empty) return g;
        if (prop?.GetValue(request) is string s && Guid.TryParse(s, out var gg)) return gg;
        return null;
    }

    private static Guid? ExtractPlayerId(TRequest request, string actorId)
    {
        var prop = request.GetType().GetProperty("PlayerId");
        if (prop?.GetValue(request) is Guid g && g != Guid.Empty) return g;
        if (Guid.TryParse(actorId, out var actorGuid)) return actorGuid;
        return null;
    }

    private static string? ExtractData(TRequest request, TResponse response)
    {
        try
        {
            var json = System.Text.Json.JsonSerializer.Serialize(request, new System.Text.Json.JsonSerializerOptions { ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles });
            if (json.Length > 1000) json = json.Substring(0, 1000);
            // Sanitize: remove IsCorrect, tokens
            if (json.Contains("IsCorrect", StringComparison.OrdinalIgnoreCase))
            {
                json = System.Text.RegularExpressions.Regex.Replace(json, "\"IsCorrect\"\\s*:\\s*[^,}]*,?", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return json;
        }
        catch
        {
            return null;
        }
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
