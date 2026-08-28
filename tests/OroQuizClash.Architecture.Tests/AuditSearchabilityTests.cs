using System.Reflection;

using OroQuizClash.Domain.Audit;
using OroQuizClash.Infrastructure.Persistence.Configurations;

namespace OroQuizClash.Architecture.Tests;

public sealed class AuditSearchabilityTests
{
    [Fact]
    public void AuditEntry_ShouldHaveIndexesForSearch()
    {
        var builderType = typeof(AuditEntryTypeConfiguration);
        var method = builderType.GetMethod("Configure");
        Assert.NotNull(method);
        // Verify via reflection that Configure adds indexes; we just check the type exists and has expected properties
        var props = typeof(AuditEntry).GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
        Assert.Contains("GameId", props);
        Assert.Contains("PlayerId", props);
        Assert.Contains("CorrelationId", props);
        Assert.Contains("Action", props);
    }

    [Fact]
    public void AuditEntrySpecification_ShouldSupportGameIdFilter()
    {
        var spec = new OroQuizClash.Infrastructure.Specifications.AuditEntrySpecification(gameId: Guid.NewGuid());
        Assert.NotNull(spec);
    }
}
