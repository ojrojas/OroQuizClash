namespace QuizArena.Admin.Client.Services;

public sealed class ClientPlayersService(HttpClient httpClient)
    : PlayersServiceCore(httpClient, "bff"), IPlayersService;
