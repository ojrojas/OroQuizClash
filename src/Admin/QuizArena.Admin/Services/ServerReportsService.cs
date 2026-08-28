using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

public sealed class ServerReportsService(HttpClient httpClient)
    : ReportsServiceCore(httpClient, "api"), IReportsService;
