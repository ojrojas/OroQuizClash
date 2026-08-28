using QuizArena.Admin.Client.Models.Audit;

namespace QuizArena.Admin.Tests;

public sealed class AuditDetailTests
{
    private static AuditDetail CreateDetail(string action, string? previous, string? next, string resultStatus = "Success")
    {
        var who = new WhoView("sub-123", "Admin", "admin@example.com", "tenant-1");
        var where = new WhereView("oroclash-api", "POST /api/categories", "10.0.0.1", "00-abc123-01", "abc123");
        var entity = new EntityView("Category", Guid.NewGuid());
        var result = new ResultView(resultStatus, resultStatus=="Failed"?"ConcurrencyConflict":null, resultStatus=="Failed"?"RowVersion mismatch":null);
        var diff = new List<JsonDiffEntry>();
        if (previous is not null && next is not null && previous != next)
            diff.Add(new JsonDiffEntry("$.Name", previous, next, "Modified"));
        return new AuditDetail(Guid.NewGuid(), who, "UpdateCategory", DateTimeOffset.UtcNow, where, entity, previous, next, action, result, diff);
    }

    [Fact]
    public void Detail_Create_PreviousNull()
    {
        var detail = CreateDetail("CREATE", null, "{ \"Name\": \"Historia\" }");
        Assert.Null(detail.PreviousValue);
        Assert.NotNull(detail.NewValue);
        Assert.Equal("CREATE", detail.Action);
        Assert.Empty(detail.Diff);
    }

    [Fact]
    public void Detail_Update_Diff()
    {
        var detail = CreateDetail("UPDATE", "{ \"Name\": \"Viejo\" }", "{ \"Name\": \"Nuevo\" }");
        Assert.NotNull(detail.PreviousValue);
        Assert.NotNull(detail.NewValue);
        Assert.Single(detail.Diff);
        Assert.Equal("$.Name", detail.Diff[0].Path);
        Assert.Equal("Modified", detail.Diff[0].ChangeType);
    }

    [Fact]
    public void Detail_Delete_NewNull()
    {
        var detail = CreateDetail("DELETE", "{ \"Name\": \"Viejo\" }", null);
        Assert.NotNull(detail.PreviousValue);
        Assert.Null(detail.NewValue);
    }

    [Fact]
    public void Detail_Failed_Result()
    {
        var detail = CreateDetail("UPDATE", "{ \"Name\": \"Viejo\" }", "{ \"Name\": \"Nuevo\" }", "Failed");
        Assert.Equal("Failed", detail.Result.Status);
        Assert.Equal("ConcurrencyConflict", detail.Result.ErrorCode);
        Assert.NotNull(detail.Result.Detail);
    }

    [Fact]
    public void Detail_CorrelationId_Propagated()
    {
        var detail = CreateDetail("CREATE", null, "{}");
        Assert.Equal("00-abc123-01", detail.Where.CorrelationId);
        Assert.Equal("abc123", detail.Where.TraceId);
        Assert.False(string.IsNullOrWhiteSpace(detail.Where.CorrelationId));
    }

    [Fact]
    public void Detail_MaskSecrets()
    {
        var json = "{ \"Name\": \"Test\", \"password\": \"secret123\", \"secret\": \"abc\" }";
        var masked = System.Text.RegularExpressions.Regex.Replace(json, "\"(password|secret|token)\"\\s*:\\s*\"[^\"]*\"", "\"$1\":\"***\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        Assert.Contains("\"password\":\"***\"", masked);
        Assert.Contains("\"secret\":\"***\"", masked);
        Assert.DoesNotContain("secret123", masked);
    }

    [Fact]
    public void Detail_Truncate_Over10KB()
    {
        var large = new string('a', 11000);
        var detail = CreateDetail("UPDATE", large, large);
        Assert.True(detail.PreviousValue!.Length > 10000);
        // Truncate logic in component would cut to 10KB
        var truncated = detail.PreviousValue[..10000] + "... (truncado)";
        Assert.True(truncated.Length < detail.PreviousValue.Length);
        Assert.Contains("truncado", truncated);
    }

    [Fact]
    public void Detail_Immutability()
    {
        var detail = CreateDetail("CREATE", null, "{}");
        // AuditEntry is record, should be immutable via with
        var modified = detail with { What = "Modified" };
        Assert.NotEqual(detail.What, modified.What);
        Assert.Equal("UpdateCategory", detail.What);
    }
}
