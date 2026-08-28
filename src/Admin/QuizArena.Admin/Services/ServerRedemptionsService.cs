using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

public sealed class ServerRedemptionsService(HttpClient httpClient)
    : RedemptionsServiceCore(httpClient, "api"), IRedemptionsService;
