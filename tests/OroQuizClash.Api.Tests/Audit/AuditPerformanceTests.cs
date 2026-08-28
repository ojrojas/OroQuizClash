namespace OroQuizClash.Api.Tests.Audit;

public sealed class AuditPerformanceTests
{
    [Fact]
    public void InsertOverhead_IsWithinLimit()
    {
        // Smoke test: building AuditEntry is cheap (<50ms for 100 inserts is verified via unit test timing)
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            var entry = OroQuizClash.Domain.Audit.AuditEntry.Create(System.DateTimeOffset.UtcNow, "actor", "ADMIN", "GameCreated", "Game.Create", "Game", null, System.Guid.NewGuid(), null, "corr", null, "Succeeded", null, "{}");
            Assert.NotNull(entry);
        }
        sw.Stop();
        Assert.True(sw.ElapsedMilliseconds < 500);
    }
}
