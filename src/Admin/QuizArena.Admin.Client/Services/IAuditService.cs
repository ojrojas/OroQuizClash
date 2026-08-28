using QuizArena.Admin.Client.Models;
using AuditModels = QuizArena.Admin.Client.Models.Audit;

namespace QuizArena.Admin.Client.Services;

public interface IAuditService
{
    Task<PagedResult<AuditEntry>> GetAuditAsync(AuditFilter filter, CancellationToken ct = default);
    Task<AuditEntry> GetAuditDetailAsync(Guid id, CancellationToken ct = default);

    // 026 Admin Audit — 9 campos
    Task<PagedResult<AuditModels.AuditEntry>> GetAuditAsync(AuditModels.AuditFilter filter, CancellationToken ct = default);
    Task<AuditModels.AuditDetail> GetAuditDetailAsync(Guid id, AuditModels.AuditFilter? contextFilter, CancellationToken ct = default);
    Task<AuditModels.AuditDetail> GetDetailAsync(Guid auditId, CancellationToken ct = default);
}
