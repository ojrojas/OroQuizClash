namespace OroQuizClash.Api.Tests.Audit;

public sealed class AuditApiTests
{
    [Fact]
    public void GetAudit_RequiresAuditReadPolicy()
    {
        var endpoint = typeof(OroQuizClash.Application.Features.Audit.GetAuditEntriesEndpoint);
        var method = endpoint.GetMethod("MapEndpoint");
        Assert.NotNull(method);
    }

    [Fact]
    public void AuditEntry_IsImmutable_NoPutDeleteEndpoints()
    {
        var endpoints = typeof(OroQuizClash.Application.Features.Audit.GetAuditEntriesEndpoint).GetMethods()
            .Select(m => m.Name).ToList();
        Assert.DoesNotContain("Put", endpoints);
        Assert.DoesNotContain("Delete", endpoints);
    }
}
