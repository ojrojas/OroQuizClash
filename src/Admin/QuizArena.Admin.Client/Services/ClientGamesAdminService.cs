namespace QuizArena.Admin.Client.Services;

/// <summary>
/// WASM implementation: calls the admin server's own BFF routes (/bff/*). The session cookie
/// travels automatically; the access_token never reaches the browser (BFF, FR-030).
/// </summary>
public sealed class ClientGamesAdminService(HttpClient httpClient)
    : GamesAdminServiceCore(httpClient, "bff"), IGamesAdminService;
