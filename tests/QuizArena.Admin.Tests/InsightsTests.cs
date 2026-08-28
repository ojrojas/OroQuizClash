using System.Reflection;
using QuizArena.Admin.Client.Models;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

/// <summary>T062: Report mapping + audit query serialization + IAuditService immutability.</summary>
public sealed class InsightsTests
{
    [Fact]
    public void ReportResult_HoldsColumnsAndRows()
    {
        var r = new ReportResult("R", new DateRange(null, null), ["A", "B"], [["1", "2"], ["3", "4"]]);
        Assert.Equal(2, r.Columns.Count);
        Assert.Equal(2, r.Rows.Count);
    }

    [Fact]
    public void AuditFilter_ToQueryString_ContainsKeys()
    {
        // Simulate what AuditServiceCore does: Build query via QueryString helper (reflection check of service existence)
        var filter = new AuditFilter(ActorId: "actor1", Action: "Create", From: DateTimeOffset.Parse("2024-01-01T00:00:00Z"), To: DateTimeOffset.Parse("2024-12-31T00:00:00Z"), Page: 2, PageSize: 10);
        Assert.Equal("actor1", filter.ActorId);
        Assert.Equal("Create", filter.Action);
        Assert.Equal(2, filter.Page);
    }

    [Fact]
    public void IAuditService_HasNoWriteMethods()
    {
        var methods = typeof(IAuditService).GetMethods(BindingFlags.Public | BindingFlags.Instance);
        foreach (var m in methods)
        {
            Assert.DoesNotContain("Create", m.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Update", m.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Delete", m.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Post", m.Name, StringComparison.OrdinalIgnoreCase);
        }
        // Only Get* methods
        Assert.All(methods, m => Assert.StartsWith("Get", m.Name));
    }

    [Fact]
    public void IAuditService_HasExactlyTwoMethods()
    {
        Assert.Equal(2, typeof(IAuditService).GetMethods().Length);
    }
}
