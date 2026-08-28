using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

/// <summary>
/// InteractiveServer implementation: calls QuizArena.Api directly via Aspire service
/// discovery; BearerTokenHandler attaches the operator's access_token per request.
/// </summary>
public sealed class ServerGamesAdminService(HttpClient httpClient)
    : GamesAdminServiceCore(httpClient, "api"), IGamesAdminService;
