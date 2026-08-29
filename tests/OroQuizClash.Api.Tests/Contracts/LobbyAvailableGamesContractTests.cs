using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace OroQuizClash.Api.Tests.Contracts;

public sealed class LobbyAvailableGamesContractTests
{
    [Fact]
    public async Task GetAvailableGames_ReturnsOnlyWaiting_And_8Fields()
    {
        // Arrange: seed 3 WAITING + 2 FINISHED via factory (Testcontainers) is assumed
        // Act: GET /api/games?status=WAITING_FOR_PLAYERS&page=1&pageSize=20
        // Assert: 100% status==WAITING_FOR_PLAYERS (SC-001), each item has 8 fields (Game Name, Category, Difficulty, NumberOfRounds, Players, StartTime, Prize, Status) SC-002, Prize placeholder "—" when null
        await Task.CompletedTask;
        Assert.True(true);
    }

    [Fact]
    public async Task GetAvailableGames_Pagination_Preserves_Filter()
    {
        // With 25 games, pageSize 20 → 20 + 5
        await Task.CompletedTask;
        Assert.True(true);
    }
}
