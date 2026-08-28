using System.Security.Claims;

namespace OroQuizClash.Application.Features.Games;

public static class GameClaims
{
    public static Guid GetSub(ClaimsPrincipal user)
    {
        var sub = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    public static bool IsOrganizer(ClaimsPrincipal user) =>
        user.HasClaim(c => c.Type == "roles" && (c.Value == "ADMIN" || c.Value == "GAME_MANAGER")) ||
        user.HasClaim(c => c.Type == "role" && (c.Value == "ADMIN" || c.Value == "GAME_MANAGER"));
}
