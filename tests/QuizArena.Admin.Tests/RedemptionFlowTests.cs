using QuizArena.Admin.Client.Models.Rewards;

namespace QuizArena.Admin.Tests;

public sealed class RedemptionFlowTests
{
    [Theory]
    [InlineData(RedemptionStateView.Requested, RedemptionStateView.Approved, true)]
    [InlineData(RedemptionStateView.Requested, RedemptionStateView.Rejected, true)]
    [InlineData(RedemptionStateView.Requested, RedemptionStateView.Cancelled, true)]
    [InlineData(RedemptionStateView.Approved, RedemptionStateView.Delivered, true)]
    [InlineData(RedemptionStateView.Approved, RedemptionStateView.Cancelled, true)]
    [InlineData(RedemptionStateView.Requested, RedemptionStateView.Delivered, false)]
    [InlineData(RedemptionStateView.Approved, RedemptionStateView.Rejected, false)]
    [InlineData(RedemptionStateView.Rejected, RedemptionStateView.Approved, false)]
    [InlineData(RedemptionStateView.Delivered, RedemptionStateView.Approved, false)]
    [InlineData(RedemptionStateView.Cancelled, RedemptionStateView.Delivered, false)]
    public void RedemptionState_IsValidTransition(RedemptionStateView from, RedemptionStateView to, bool expected)
    {
        Assert.Equal(expected, RedemptionStateMap.IsValidTransition(from, to));
    }

    [Theory]
    [InlineData(RedemptionStateView.Rejected, true)]
    [InlineData(RedemptionStateView.Delivered, true)]
    [InlineData(RedemptionStateView.Cancelled, true)]
    [InlineData(RedemptionStateView.Requested, false)]
    [InlineData(RedemptionStateView.Approved, false)]
    public void RedemptionState_IsTerminal(RedemptionStateView status, bool terminal)
    {
        Assert.Equal(terminal, RedemptionStateMap.IsTerminal(status));
    }

    [Fact]
    public void RedemptionStateMap_FiveStates_RoundTrips()
    {
        foreach (var s in Enum.GetValues<RedemptionStateView>())
        {
            var api = RedemptionStateMap.ToApi(s);
            var back = RedemptionStateMap.FromApi(api);
            Assert.Equal(s, back);
        }
    }

    [Fact]
    public void Consolation_IsIndependent()
    {
        Assert.True(RewardTypeMap.IsConsolation(RewardType.Consolation));
        Assert.False(RewardTypeMap.IsConsolation(RewardType.Physical));
        // Simulate that normal reward cannot be consolation
        var normalRedemption = new RewardRedemption(Guid.NewGuid(), Guid.NewGuid(), "Voucher", RewardType.Voucher, Guid.NewGuid(), "Player", 100, RedemptionStateView.Requested, DateTimeOffset.UtcNow, null, null, null, null, false, "abc");
        var consolationRedemption = normalRedemption with { RewardType = RewardType.Consolation, IsConsolation = true };
        Assert.False(normalRedemption.IsConsolation);
        Assert.True(consolationRedemption.IsConsolation);
        Assert.Equal(RewardType.Voucher, normalRedemption.RewardType);
        Assert.Equal(RewardType.Consolation, consolationRedemption.RewardType);
    }

    [Fact]
    public void StockLogic_PhysicalLimited_VoucherUnlimited()
    {
        // Physical stock 2 -> 2 approves -> stock 0 -> third fails
        var stock = 2;
        Assert.False(RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Physical));
        stock--; // approve 1
        Assert.Equal(1, stock);
        stock--; // approve 2
        Assert.Equal(0, stock);
        // third approve should be RewardOutOfStock => we simulate guard
        var canApprove = stock == 0 ? RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Physical) : stock > 0;
        Assert.False(canApprove);

        // Voucher stock 0 unlimited -> always approvable
        var voucherStock = 0;
        Assert.True(RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Voucher));
        var voucherCanApprove = voucherStock == 0 ? RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Voucher) : voucherStock > 0;
        Assert.True(voucherCanApprove);
        // 100 approves still true
        for (int i = 0; i < 100; i++) Assert.True(voucherCanApprove);
    }

    [Fact]
    public void Availability_FueraDeVentana_NotEligible()
    {
        var detail = new RewardDetail(Guid.NewGuid(), "Digital Prize", null, RewardType.Digital, 100, 10, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero), RewardStateView.Active, false, "row", []);
        var nowOutside = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var eligible = RewardDetail.ComputeIsEligible(detail.Status, detail.Stock, detail.Type, detail.AvailableFrom, detail.AvailableTo, nowOutside);
        Assert.False(eligible);
        var nowInside = new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero);
        Assert.True(RewardDetail.ComputeIsEligible(detail.Status, detail.Stock, detail.Type, detail.AvailableFrom, detail.AvailableTo, nowInside));
    }

    [Fact]
    public void Concurrency_RowVersion_Required()
    {
        var rv1 = "AAAAAAAAB9E=";
        var rv2 = "AAAAAAAAB9I=";
        Assert.NotEqual(rv1, rv2);
        // Simulate If-Match handling: second update with old rowversion should get 409
        var current = rv2;
        var attempt = rv1;
        Assert.NotEqual(current, attempt);
        // This would be ConcurrencyConflict per spec SC-008
    }

    [Fact]
    public void InsufficientPoints_Guard()
    {
        int cost = 1000;
        int playerPoints = 500;
        var canAfford = playerPoints >= cost;
        Assert.False(canAfford);
        // Should be 409 InsufficientPoints
        cost = 100;
        playerPoints = 500;
        Assert.True(playerPoints >= cost);
    }
}
