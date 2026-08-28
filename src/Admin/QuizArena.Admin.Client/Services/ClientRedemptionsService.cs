namespace QuizArena.Admin.Client.Services;

public sealed class ClientRedemptionsService(HttpClient httpClient)
    : RedemptionsServiceCore(httpClient, "bff"), IRedemptionsService;
