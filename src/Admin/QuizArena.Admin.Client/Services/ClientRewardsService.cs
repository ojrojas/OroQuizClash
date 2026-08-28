namespace QuizArena.Admin.Client.Services;

public sealed class ClientRewardsService(HttpClient httpClient)
    : RewardsServiceCore(httpClient, "bff"), IRewardsService;
