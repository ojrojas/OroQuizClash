using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Services;

public interface IAuditService
{
    Task<PagedResult<AuditEntry>> GetAuditAsync(AuditFilter filter, CancellationToken ct = default);
    Task<AuditEntry> GetAuditDetailAsync(Guid id, CancellationToken ct = default);
}
