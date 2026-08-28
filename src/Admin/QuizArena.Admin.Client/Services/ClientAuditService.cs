namespace QuizArena.Admin.Client.Services;

public sealed class ClientAuditService(HttpClient httpClient)
    : AuditServiceCore(httpClient, "bff"), IAuditService;
