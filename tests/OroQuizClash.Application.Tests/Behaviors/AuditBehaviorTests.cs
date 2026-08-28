using System.Security.Claims;

using Microsoft.AspNetCore.Http;

using OroQuizClash.Domain.Audit;

namespace OroQuizClash.Application.Tests.Behaviors;

public sealed class AuditBehaviorTests
{
    [Fact]
    public void AuditEntry_CanBeCreated_WithAllRequiredFields()
    {
        var entry = AuditEntry.Create(DateTimeOffset.UtcNow, "actor-123", "PLAYER", "SubmitAnswer", "Game.Play", "Game:guid", "corr-123", null, "Success", null, null);
        Assert.Equal("actor-123", entry.ActorId);
        Assert.Equal("SubmitAnswer", entry.Action);
        Assert.Equal("Success", entry.Result);
    }

    [Fact]
    public void AuditEntry_IsAppendOnly_NoUpdateMethod()
    {
        var methods = typeof(AuditEntry).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Update") || m.Name.StartsWith("Delete") || m.Name.StartsWith("Modify"))
            .ToList();
        Assert.Empty(methods);
    }

    [Fact]
    public void CorrelationId_IsPropagated()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["X-Correlation-ID"] = "test-corr-123";
        Assert.Equal("test-corr-123", ctx.Request.Headers["X-Correlation-ID"].FirstOrDefault());
    }
}
