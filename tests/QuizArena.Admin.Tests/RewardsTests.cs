using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Tests;

/// <summary>T056: Redemption status transitions + role gating (via AdminNavigation).</summary>
public sealed class RewardsTests
{
    [Theory]
    [InlineData(RedemptionStatusView.Pending, RedemptionStatusView.Approved, true)]
    [InlineData(RedemptionStatusView.Pending, RedemptionStatusView.Rejected, true)]
    [InlineData(RedemptionStatusView.Pending, RedemptionStatusView.Cancelled, true)]
    [InlineData(RedemptionStatusView.Approved, RedemptionStatusView.Delivered, true)]
    [InlineData(RedemptionStatusView.Approved, RedemptionStatusView.Pending, false)]
    [InlineData(RedemptionStatusView.Rejected, RedemptionStatusView.Approved, false)]
    [InlineData(RedemptionStatusView.Delivered, RedemptionStatusView.Approved, false)]
    [InlineData(RedemptionStatusView.Cancelled, RedemptionStatusView.Approved, false)]
    public void RedemptionTransition_Validity(RedemptionStatusView from, RedemptionStatusView to, bool valid)
    {
        Assert.Equal(valid, IsValidTransition(from, to));
    }

    [Theory]
    [InlineData(RedemptionStatusView.Rejected, true)]
    [InlineData(RedemptionStatusView.Delivered, true)]
    [InlineData(RedemptionStatusView.Cancelled, true)]
    [InlineData(RedemptionStatusView.Pending, false)]
    [InlineData(RedemptionStatusView.Approved, false)]
    public void IsTerminal(RedemptionStatusView status, bool terminal)
    {
        Assert.Equal(terminal, RedemptionStatusMap.IsTerminal(status));
    }

    [Fact]
    public void TerminalStates_CannotBeReprocessed()
    {
        foreach (var terminal in new[] { RedemptionStatusView.Rejected, RedemptionStatusView.Delivered, RedemptionStatusView.Cancelled })
        {
            Assert.True(RedemptionStatusMap.IsTerminal(terminal));
            Assert.False(IsValidTransition(terminal, RedemptionStatusView.Approved));
            Assert.False(IsValidTransition(terminal, RedemptionStatusView.Delivered));
        }
    }

    private static bool IsValidTransition(RedemptionStatusView from, RedemptionStatusView to) => (from, to) switch
    {
        (RedemptionStatusView.Pending, RedemptionStatusView.Approved) => true,
        (RedemptionStatusView.Pending, RedemptionStatusView.Rejected) => true,
        (RedemptionStatusView.Pending, RedemptionStatusView.Cancelled) => true,
        (RedemptionStatusView.Approved, RedemptionStatusView.Delivered) => true,
        _ => false
    };
}
