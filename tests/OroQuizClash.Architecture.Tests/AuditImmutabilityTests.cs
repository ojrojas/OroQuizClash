using System.Reflection;

using OroQuizClash.Domain.Audit;
using OroQuizClash.Infrastructure.Persistence;

namespace OroQuizClash.Architecture.Tests;

public sealed class AuditImmutabilityTests
{
    [Fact]
    public void AuditEntry_HasNoPublicUpdateOrDelete()
    {
        var methods = typeof(AuditEntry).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name.StartsWith("Update") || m.Name == "Delete" || m.Name.StartsWith("Set"))
            .Where(m => m.DeclaringType == typeof(AuditEntry))
            .ToList();
        Assert.Empty(methods);
    }

    [Fact]
    public void DbContext_ExposesAuditEntriesAsDbSet()
    {
        var prop = typeof(OroQuizClashDbContext).GetProperty("AuditEntries");
        Assert.NotNull(prop);
        Assert.Equal(typeof(Microsoft.EntityFrameworkCore.DbSet<AuditEntry>), prop!.PropertyType);
    }

    [Fact]
    public void DbContext_ExposesIdempotencyRecordsAsDbSet()
    {
        var prop = typeof(OroQuizClashDbContext).GetProperty("IdempotencyRecords");
        Assert.NotNull(prop);
    }
}
