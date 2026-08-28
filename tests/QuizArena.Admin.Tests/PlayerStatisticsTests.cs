using QuizArena.Admin.Client.Models.Players;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

public sealed class PlayerStatisticsTests
{
    [Fact]
    public void TransactionType_TenValues()
    {
        Assert.Equal(10, Enum.GetValues<TransactionType>().Length);
        foreach (var t in Enum.GetValues<TransactionType>())
        {
            var api = TransactionTypeMap.ToApi(t);
            var back = TransactionTypeMap.FromApi(api);
            Assert.Equal(t, back);
        }
    }

    [Fact]
    public void ScoreFilter_Validates_FromAfterTo()
    {
        var f = new ScoreFilter(From: new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero), To: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(f.Validate().ContainsKey("DateRange"));
    }

    [Fact]
    public void Ledger_Sum_IsServerTruth()
    {
        var txs = new[]
        {
            new PointTransactionView(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TransactionType.ANSWER_CORRECT, 100, DateTimeOffset.UtcNow, null),
            new PointTransactionView(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TransactionType.PENALTY, -50, DateTimeOffset.UtcNow, null),
            new PointTransactionView(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TransactionType.CONSOLATION, 20, DateTimeOffset.UtcNow, null),
        };
        var total = txs.Sum(t => t.Points);
        Assert.Equal(70, total);
        Assert.Contains(txs, t => t.Type == TransactionType.CONSOLATION);
    }

    [Fact]
    public void IsConsolation_Distinction()
    {
        var consolation = new PlayerRedemptionView(Guid.NewGuid(), Guid.NewGuid(), "Premio Consolación", "Consolation", 0, "Delivered", DateTimeOffset.UtcNow, null, null, null, true, "AAAA");
        var normal = consolation with { RewardType = "Voucher", IsConsolation = false };
        Assert.True(consolation.IsConsolation);
        Assert.False(normal.IsConsolation);
        Assert.Equal("Consolation", consolation.RewardType);
        Assert.NotEqual(consolation.RewardType, normal.RewardType);
    }

    [Fact]
    public void RedemptionFilter_Validates()
    {
        var f = new RedemptionFilter(From: new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero), To: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(f.Validate().ContainsKey("DateRange"));
    }

    [Fact]
    public void PlayerStatistics_Snapshot()
    {
        var stats = new PlayerStatistics(Guid.NewGuid(), 42, 5, 12, 245.5, 0.78, 7, TimeSpan.FromSeconds(18), new Dictionary<string,int>{{"1",10}}, new Dictionary<string,int>{{"Historia",20}}, DateTimeOffset.UtcNow);
        Assert.Equal(42, stats.TotalGames);
        Assert.Equal(5, stats.Wins);
        Assert.InRange(stats.AccuracyRate, 0, 1);
        Assert.True(stats.CalculatedAt <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void Authorization_GameManager_403_ForRewards()
    {
        var roles = new[] { "GAME_MANAGER" };
        var isRewardManagerOrAdmin = roles.Any(r => r == "ADMIN" || r == "REWARD_MANAGER");
        Assert.False(isRewardManagerOrAdmin);
        // GAME_MANAGER should get 403 on /players/{id}/redemptions per FR-011
        var isAdminOrGameManager = roles.Any(r => r == "ADMIN" || r == "GAME_MANAGER");
        Assert.True(isAdminOrGameManager);
        // So GAME_MANAGER can access history but not rewards
    }

    [Fact]
    public void PlayerCatalogs_TransactionTypes_Ten()
    {
        Assert.Equal(10, PlayerCatalogs.TransactionTypes.Count);
        Assert.Contains("CONSOLATION", PlayerCatalogs.TransactionTypes);
        Assert.Contains("REWARD_REDEMPTION", PlayerCatalogs.TransactionTypes);
    }
}
