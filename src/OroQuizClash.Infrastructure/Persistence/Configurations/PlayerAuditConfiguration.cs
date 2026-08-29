using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OroQuizClash.Domain.Audit;

namespace OroQuizClash.Infrastructure.Persistence.Configurations;

// T040: Audit trail for player events (union, answer, score/secured, status, impersonation attempt)
// Verified existing AuditEntry append-only; this file documents union of player-specific audit fields
public sealed class PlayerAuditConfiguration : IEntityTypeConfiguration<AuditEntry>
{
    public void Configure(EntityTypeBuilder<AuditEntry> builder)
    {
        // AuditEntry already append-only with GameId/PlayerId/RoundId/QuestionId/CorrelationId/TraceId
        // Player events: GamePlayer Joined, Answer Submitted/Evaluated, ScoreUpdated, Withdrawn/Eliminated, ImpersonationAttempt
        builder.HasIndex(e => new { e.Action, e.Resource });
        builder.HasIndex(e => e.CorrelationId);
        builder.HasIndex(e => e.GameId);
        // Ensure append-only: no updates/deletes via application logic (enforced by repository pattern)
    }
}
