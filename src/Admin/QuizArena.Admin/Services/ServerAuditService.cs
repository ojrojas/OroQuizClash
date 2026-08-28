using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

public sealed class ServerAuditService(HttpClient httpClient)
    : AuditServiceCore(httpClient, "api"), IAuditService;
