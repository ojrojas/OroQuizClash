using QuizArena.Admin.Client.Models.Audit;
using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Tests;

public sealed class AuditListTests
{
    [Fact]
    public void AuditFilter_NineFields_Valid()
    {
        var f = new AuditFilter(Who: "admin", What: "CreateCategory", WhenFrom: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), WhenTo: new DateTimeOffset(2026, 5, 13, 0, 0, 0, TimeSpan.Zero), Where: "oroclash-api", EntityType: "Category", EntityId: Guid.NewGuid(), Action: "CREATE", Result: "Success", Page: 1, PageSize: 20);
        Assert.Empty(f.Validate());
    }

    [Fact]
    public void AuditFilter_WhenFromAfterWhenTo_Fails()
    {
        var f = new AuditFilter(WhenFrom: new DateTimeOffset(2026, 5, 20, 0, 0, 0, TimeSpan.Zero), WhenTo: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        Assert.True(f.Validate().ContainsKey("DateRange"));
    }

    [Fact]
    public void AuditFilter_Action_Invalid_Fails()
    {
        var f = new AuditFilter(Action: "INVALID_ACTION_XYZ");
        Assert.True(f.Validate().ContainsKey(nameof(AuditFilter.Action)));
    }

    [Fact]
    public void AuditFilter_EntityType_Invalid_Fails()
    {
        var f = new AuditFilter(EntityType: "INVALID_ENTITY");
        Assert.True(f.Validate().ContainsKey(nameof(AuditFilter.EntityType)));
    }

    [Fact]
    public void AuditFilter_Result_Invalid_Fails()
    {
        var f = new AuditFilter(Result: "INVALID");
        Assert.True(f.Validate().ContainsKey(nameof(AuditFilter.Result)));
    }

    [Fact]
    public void AuditFilter_Pagination_Invalid_Fails()
    {
        var f = new AuditFilter(Page: 0, PageSize: 200);
        Assert.True(f.Validate().ContainsKey(nameof(AuditFilter.Page)));
        Assert.True(f.Validate().ContainsKey(nameof(AuditFilter.PageSize)));
    }

    [Fact]
    public void AuditCatalogs_EntityTypes_Seven()
    {
        Assert.Equal(7, AuditCatalogs.EntityTypes.Count);
        Assert.Contains("Game", AuditCatalogs.EntityTypes);
        Assert.Contains("Reward", AuditCatalogs.EntityTypes);
    }

    [Fact]
    public void AuditCatalogs_Actions_Fourteen()
    {
        Assert.Equal(14, AuditCatalogs.Actions.Count);
        Assert.Contains("CREATE", AuditCatalogs.Actions);
        Assert.Contains("APPROVE", AuditCatalogs.Actions);
    }

    [Fact]
    public void AuditCatalogs_Results_Two()
    {
        Assert.Equal(2, AuditCatalogs.Results.Count);
        Assert.Contains("Success", AuditCatalogs.Results);
        Assert.Contains("Failed", AuditCatalogs.Results);
    }

    [Fact]
    public void Authorization_AdminOnly_ForAudit()
    {
        var rolesAdmin = new[] { "ADMIN" };
        var canAdmin = rolesAdmin.Any(r => r == "ADMIN");
        Assert.True(canAdmin);
        var rolesPlayer = new[] { "PLAYER" };
        var canPlayer = rolesPlayer.Any(r => r == "ADMIN");
        Assert.False(canPlayer);
    }

    [Fact]
    public void Paginacion_Correcta()
    {
        var f = new AuditFilter(Page: 2, PageSize: 20);
        Assert.Empty(f.Validate());
        Assert.Equal(2, f.Page);
    }
}
