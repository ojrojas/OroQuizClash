using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

public sealed class ServerPlayersService(HttpClient httpClient)
    : PlayersServiceCore(httpClient, "api"), IPlayersService;
