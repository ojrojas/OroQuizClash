using System.Reflection;

using OroQuizClash.Domain.Audit;

namespace OroQuizClash.Domain.Tests.Audit;

public sealed class AuditEntryImmutabilityTests
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
    public void AuditEntry_Create_SetsTimestampServerUtc()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        var entry = AuditEntry.Create(DateTimeOffset.UtcNow, "actor", "ADMIN", "GameCreated", "Game.Create", "Game", Guid.NewGuid().ToString(), Guid.NewGuid(), null, "corr", null, "Succeeded", null, "{\"name\":\"test\"}");
        Assert.True(entry.Timestamp >= before);
        Assert.True(entry.Timestamp <= DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void AuditEntry_WithGameIdAndPlayerId_Persists()
    {
        var gameId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var entry = AuditEntry.Create(DateTimeOffset.UtcNow, "actor", "PLAYER", "AnswerSubmitted", "Game.Play", "Answer", Guid.NewGuid().ToString(), gameId, playerId, "corr", null, "Succeeded", null, "{}");
        Assert.Equal(gameId, entry.GameId);
        Assert.Equal(playerId, entry.PlayerId);
    }
}
