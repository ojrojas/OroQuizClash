using QuizArena.Admin.Client.Models.Rewards;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

public sealed class RewardCatalogValidationTests
{
    private static RewardForm ValidForm() => new("Voucher Amazon 20€", "Tarjeta regalo digital", RewardType.Voucher, 100, 10, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero));

    [Fact]
    public void Validate_AllSevenFields_Valid()
    {
        var form = ValidForm();
        Assert.Empty(form.Validate());
        Assert.True(form.IsValid);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_NameTooShort_Fails(string name)
    {
        var form = ValidForm() with { Name = name };
        Assert.True(form.Validate().ContainsKey(nameof(RewardForm.Name)));
    }

    [Fact]
    public void Validate_NameTooLong_Fails()
    {
        var form = ValidForm() with { Name = new string('a', 101) };
        Assert.True(form.Validate().ContainsKey(nameof(RewardForm.Name)));
    }

    [Fact]
    public void Validate_DescriptionTooLong_Fails()
    {
        var form = ValidForm() with { Description = new string('a', 501) };
        Assert.True(form.Validate().ContainsKey(nameof(RewardForm.Description)));
    }

    [Fact]
    public void Validate_TypeOutOfRange_Fails()
    {
        var form = ValidForm() with { Type = (RewardType)99 };
        Assert.True(form.Validate().ContainsKey(nameof(RewardForm.Type)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(100001)]
    public void Validate_CostOutOfRange_Fails(int cost)
    {
        var form = ValidForm() with { Cost = cost };
        Assert.True(form.Validate().ContainsKey(nameof(RewardForm.Cost)));
    }

    [Fact]
    public void Validate_CostBoundaries_Pass()
    {
        Assert.Empty((ValidForm() with { Cost = 1 }).Validate());
        Assert.Empty((ValidForm() with { Cost = 100000 }).Validate());
    }

    [Fact]
    public void Validate_StockNegative_Fails()
    {
        var form = ValidForm() with { Stock = -1 };
        Assert.True(form.Validate().ContainsKey(nameof(RewardForm.Stock)));
    }

    [Fact]
    public void Validate_StockZero_Pass()
    {
        var form = ValidForm() with { Stock = 0 };
        Assert.Empty(form.Validate());
    }

    [Fact]
    public void Validate_Availability_FromAfterTo_Fails()
    {
        var form = ValidForm() with { AvailableFrom = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), AvailableTo = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero) };
        Assert.True(form.Validate().ContainsKey("Availability"));
    }

    [Fact]
    public void Validate_Availability_FromEqualsTo_Fails()
    {
        var d = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero);
        var form = ValidForm() with { AvailableFrom = d, AvailableTo = d };
        Assert.True(form.Validate().ContainsKey("Availability"));
    }

    [Fact]
    public void Validate_Availability_OnlyOneDate_Pass()
    {
        var form = ValidForm() with { AvailableFrom = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), AvailableTo = null };
        Assert.Empty(form.Validate());
        form = ValidForm() with { AvailableFrom = null, AvailableTo = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero) };
        Assert.Empty(form.Validate());
    }

    [Fact]
    public void RewardTypeMap_SixTypes_RoundTrips()
    {
        foreach (var type in Enum.GetValues<RewardType>())
        {
            var api = RewardTypeMap.ToApi(type);
            var back = RewardTypeMap.FromApi(api);
            Assert.Equal(type, back);
        }
        Assert.Equal(6, RewardCatalogs.Types.Count);
        Assert.Contains("Consolation", RewardCatalogs.Types);
    }

    [Fact]
    public void RewardCatalogs_CostValidation()
    {
        Assert.True(RewardCatalogs.IsValidCost(1));
        Assert.True(RewardCatalogs.IsValidCost(50000));
        Assert.True(RewardCatalogs.IsValidCost(100000));
        Assert.False(RewardCatalogs.IsValidCost(0));
        Assert.False(RewardCatalogs.IsValidCost(100001));
    }

    [Fact]
    public void RewardCatalogs_StockValidation()
    {
        Assert.True(RewardCatalogs.IsValidStock(0));
        Assert.True(RewardCatalogs.IsValidStock(10));
        Assert.False(RewardCatalogs.IsValidStock(-1));
    }

    [Fact]
    public void RewardCatalogs_AvailabilityValidation()
    {
        Assert.True(RewardCatalogs.IsValidAvailability(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(RewardCatalogs.IsValidAvailability(new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.True(RewardCatalogs.IsValidAvailability(null, null));
    }

    [Fact]
    public void RewardStateMap_ThreeStates()
    {
        Assert.Equal(RewardStateView.Active, RewardStateMap.FromApi("ACTIVE"));
        Assert.Equal(RewardStateView.Inactive, RewardStateMap.FromApi("INACTIVE"));
        Assert.Equal(RewardStateView.Archived, RewardStateMap.FromApi("ARCHIVED"));
        Assert.True(RewardStateMap.IsTerminal(RewardStateView.Archived));
        Assert.False(RewardStateMap.IsTerminal(RewardStateView.Active));
        Assert.True(RewardStateMap.CanEdit(RewardStateView.Active));
        Assert.False(RewardStateMap.CanEdit(RewardStateView.Archived));
    }

    [Fact]
    public void StockUnlimitedLogic()
    {
        Assert.True(RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Digital));
        Assert.True(RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Voucher));
        Assert.True(RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Consolation));
        Assert.True(RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Experience));
        Assert.False(RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Physical));
        Assert.False(RewardTypeMap.IsStockUnlimitedAllowed(RewardType.Monetary));
    }

    [Fact]
    public void IsEligible_Calculation()
    {
        var now = new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        Assert.True(RewardDetail.ComputeIsEligible(RewardStateView.Active, 10, RewardType.Physical, null, null, now));
        Assert.False(RewardDetail.ComputeIsEligible(RewardStateView.Inactive, 10, RewardType.Physical, null, null, now));
        Assert.False(RewardDetail.ComputeIsEligible(RewardStateView.Active, 0, RewardType.Physical, null, null, now));
        Assert.True(RewardDetail.ComputeIsEligible(RewardStateView.Active, 0, RewardType.Digital, null, null, now));
        Assert.False(RewardDetail.ComputeIsEligible(RewardStateView.Active, 10, RewardType.Physical, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 30, 0, 0, 0, TimeSpan.Zero), now));
        Assert.True(RewardDetail.ComputeIsEligible(RewardStateView.Active, 10, RewardType.Physical, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero), now));
    }
}
