using OroQuizClash.Domain.Audit;

namespace OroQuizClash.Domain.Tests.Audit;

public sealed class AuditActionCatalogTests
{
    [Fact]
    public void All_Contains16Actions()
    {
        Assert.Equal(16, AuditAction.All.Count);
    }

    [Fact]
    public void ContainsExpectedActions()
    {
        var names = AuditAction.All.Select(a => a.Name).ToList();
        Assert.Contains("GameCreated", names);
        Assert.Contains("GameConfigured", names);
        Assert.Contains("GameStarted", names);
        Assert.Contains("PlayerJoined", names);
        Assert.Contains("RoundStarted", names);
        Assert.Contains("QuestionPresented", names);
        Assert.Contains("AnswerSubmitted", names);
        Assert.Contains("AnswerEvaluated", names);
        Assert.Contains("PointsAwarded", names);
        Assert.Contains("PointsRemoved", names);
        Assert.Contains("PlayerWithdrawn", names);
        Assert.Contains("PlayerEliminated", names);
        Assert.Contains("GameFinished", names);
        Assert.Contains("RewardRedeemed", names);
        Assert.Contains("ConsolationGranted", names);
        Assert.Contains("AdministrativeAdjustment", names);
    }

    [Fact]
    public void FromName_Works()
    {
        var action = AuditAction.All.First(a => a.Name == "GameCreated");
        Assert.Equal(1, action.Id);
    }
}
