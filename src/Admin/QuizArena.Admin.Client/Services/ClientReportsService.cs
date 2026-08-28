namespace QuizArena.Admin.Client.Services;

public sealed class ClientReportsService(HttpClient httpClient)
    : ReportsServiceCore(httpClient, "bff"), IReportsService;
