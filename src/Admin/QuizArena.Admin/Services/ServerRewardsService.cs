using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

public sealed class ServerRewardsService(HttpClient httpClient)
    : RewardsServiceCore(httpClient, "api"), IRewardsService;
