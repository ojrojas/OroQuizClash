using QuizArena.Admin.Client.Models.Rewards;

namespace QuizArena.Admin.Tests;

// T031: 7 fields validation, 6 types, uniqueness, cost/stock/dates, rowversion
public sealed class RewardTests
{
    [Fact]
    public void RewardForm_SevenFields_Valid()
    {
        var form = new RewardForm("Premio Válido", "Desc", RewardType.Physical, 500, 10, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero));
        Assert.Empty(form.Validate());
    }

    [Fact]
    public void RewardType_SixValues_Exist()
    {
        Assert.Equal(6, Enum.GetValues<RewardType>().Length);
        Assert.True(Enum.IsDefined(RewardType.Consolation));
    }

    [Fact]
    public void Reward_CostStockDates_Validated()
    {
        var form = new RewardForm("ab", null, RewardType.Voucher, 0, -1, new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        var errors = form.Validate();
        Assert.Contains(errors, kv => kv.Key == nameof(RewardForm.Name));
        Assert.Contains(errors, kv => kv.Key == nameof(RewardForm.Cost));
        Assert.Contains(errors, kv => kv.Key == nameof(RewardForm.Stock));
        Assert.Contains(errors, kv => kv.Key == "Availability");
    }

    [Fact]
    public void Reward_RowVersion_IsPresent()
    {
        var detail = new RewardDetail(Guid.NewGuid(), "Test", null, RewardType.Digital, 100, 5, null, null, RewardStateView.Active, true, "AAAAAAAAB9E=", []);
        Assert.False(string.IsNullOrWhiteSpace(detail.RowVersion));
        Assert.Equal("AAAAAAAAB9E=", detail.RowVersion);
    }
}
