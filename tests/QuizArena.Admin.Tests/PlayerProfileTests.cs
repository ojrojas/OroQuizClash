using QuizArena.Admin.Client.Models.Players;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

public sealed class PlayerProfileTests
{
    private static PlayerSummary ValidPlayer() => new(Guid.NewGuid(), "Ana García", "ana@example.com", "tenant-1", "DNI", "12345678A", DateTimeOffset.UtcNow.AddMonths(-2), DateTimeOffset.UtcNow, PlayerStateView.Active);

    [Fact]
    public void PlayerFilter_Search_Valid()
    {
        var f = new PlayerFilter(Search: "ana", Page: 1, PageSize: 20);
        Assert.Empty(f.Validate());
    }

    [Fact]
    public void PlayerFilter_SearchTooLong_Fails()
    {
        var f = new PlayerFilter(Search: new string('a', 101));
        Assert.True(f.Validate().ContainsKey(nameof(PlayerFilter.Search)));
    }

    [Fact]
    public void PlayerFilter_Pagination_Invalid_Fails()
    {
        var f = new PlayerFilter(Page: 0, PageSize: 200);
        var errors = f.Validate();
        Assert.Contains(errors, kv => kv.Key == nameof(PlayerFilter.Page));
        Assert.Contains(errors, kv => kv.Key == nameof(PlayerFilter.PageSize));
    }

    [Fact]
    public void GameHistoryFilter_FromAfterTo_Fails()
    {
        var f = new GameHistoryFilter(From: new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero), To: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(f.Validate().ContainsKey("DateRange"));
    }

    [Fact]
    public void PlayerStateMap_FourStates()
    {
        Assert.Equal(PlayerStateView.Active, PlayerStateMap.FromApi("Active"));
        Assert.Equal(PlayerStateView.InGame, PlayerStateMap.FromApi("InGame"));
        Assert.Equal(PlayerStateView.Withdrawn, PlayerStateMap.FromApi("Withdrawn"));
        Assert.Equal(PlayerStateView.Inactive, PlayerStateMap.FromApi("Inactive"));
        Assert.Equal(4, PlayerCatalogs.PlayerStates.Count);
    }

    [Fact]
    public void PlayerDetail_ContainsScoreSummary()
    {
        var detail = new PlayerDetail(Guid.NewGuid(), "Ana", "ana@example.com", "t1", "DNI", "123", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, PlayerStateView.Active, new PlayerScoreSummary(2450, 1200, 1250), 42, "AAAAAAAAB9E=");
        Assert.Equal(2450, detail.ScoreSummary.TotalPoints);
        Assert.Equal(42, detail.TotalParticipations);
        Assert.Equal("AAAAAAAAB9E=", detail.RowVersion);
    }

    [Fact]
    public void PlayerSummary_MapsCorrectly()
    {
        var p = ValidPlayer();
        Assert.Equal("Ana García", p.DisplayName);
        Assert.Equal("ana@example.com", p.Email);
        Assert.Equal(PlayerStateView.Active, p.State);
    }

    [Fact]
    public void ParticipationFilter_FromAfterTo_Fails()
    {
        var f = new ParticipationFilter(From: new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero), To: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(f.Validate().ContainsKey("DateRange"));
    }

    [Fact]
    public void PlayerCatalogs_GameStatuses_Nine()
    {
        Assert.Equal(9, PlayerCatalogs.GameStatuses.Count);
        Assert.Contains("FINISHED", PlayerCatalogs.GameStatuses);
        Assert.Contains("FORCED_FINISHED", PlayerCatalogs.GameStatuses);
    }
}
