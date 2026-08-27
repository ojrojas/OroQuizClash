# Research: Answer Evaluation

**Feature**: `006-answer-evaluation` | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Research Areas

### R-001: Idempotency Strategy for Answer Submissions

**Decision**: Use `UNIQUE (GameId, PlayerId, RoundId)` database constraint + `rowversion` on `Game` aggregate root.

**Rationale**: The spec requires idempotency by `PlayerId + RoundId` (FR-007). A unique constraint on `(GameId, PlayerId, RoundId)` in the `Answers` table prevents duplicate `Answer` creation at the database level. Combined with `rowversion` on the `Game` aggregate root, concurrent submissions are rejected with `409 Conflict`. The existing `SubmitAnswer.cs` uses an in-memory `HashSet` (demo only) — the real implementation must persist this.

**Alternatives considered**:
- In-memory cache (Redis) for idempotency keys: Rejected — adds infrastructure dependency for a problem already solved by DB constraint + optimistic concurrency.
- Separate `IdempotencyKey` column: Rejected — `PlayerId + RoundId` is the natural key; no need for synthetic keys.
- Application-level check before insert: Rejected — race window exists; DB constraint is the authoritative guard.

### R-002: Answer Entity Composition within Game Aggregate

**Decision**: `Answer` as `Entity<AnswerId>` composition within `Game` aggregate root, stored in separate `Answers` table with `GameId` FK.

**Rationale**: The spec defines `Answer` as part of the `Game` aggregate (FR-014). The existing pattern shows `GameRound` and `GamePlayer` as composition within `Game` using `HasMany` with backing fields. Following the same pattern for `Answer` maintains consistency. The `Game.RowVersion` protects all composition entities.

**Alternatives considered**:
- Separate `Answer` aggregate with its own `IRepository`: Rejected — breaks composition boundary; `Answer` lifecycle is entirely managed by `Game.SubmitAnswer()`.
- Value Object instead of Entity: Rejected — `Answer` has identity (`AnswerId`), mutable state transitions, and is independently queryable.

### R-003: PointTransaction as Append-Only Ledger

**Decision**: `PointTransaction` as `Entity<PointTransactionId>` composition within `Game`, append-only (no Update/Delete operations).

**Rationale**: Constitution Principle D mandates scoring via ledger. The spec requires `PointTransaction` created only via `CalculateResult` when `Answer.Status == EVALUATED` (FR-008). Append-only is enforced by not exposing Update/Delete methods on the entity. The existing `GameConfiguration.ScoringSystem` provides `PointsPerRound` and `DifficultyMultiplier` base.

**Alternatives considered**:
- Direct `Score` balance mutation: Rejected — violates Constitution Principle D (ledger mandatory).
- Separate `Score` aggregate with transaction log: Rejected — over-engineering for v1; `PointTransaction` in `Game` composition is sufficient.

### R-004: ServerTimestamp Calculation

**Decision**: Use `DateTimeOffset.UtcNow` at handler entry as `ServerTimestamp`, compute `ElapsedTime = min(ServerTimestamp - Round.StartedAt, Round.TimeLimit)`.

**Rationale**: FR-004 specifies `elapsedTime = min(ServerTimestamp - Round.StartedAt, Round.TimeLimit)`. Using `DateTimeOffset.UtcNow` at handler start ensures a single consistent timestamp for the entire validation chain. The `min()` clamp ensures `ElapsedTime` never exceeds `TimeLimit`. If `ServerTimestamp - Round.StartedAt > TimeLimit`, the answer is `EXPIRED` (FR-013).

**Alternatives considered**:
- Per-step timestamps: Rejected — adds complexity without benefit; single timestamp at entry is sufficient.
- `DateTime.UtcNow` (UTC): Rejected — `DateTimeOffset` preserves timezone info and is the .NET best practice.

### R-005: DifficultyMultiplier Calculation

**Decision**: Compute `DifficultyMultiplier` from `GameRound.Difficulty` using a configurable mapping (default: `1.0 + (difficulty - 1) * 0.25` → 1.0, 1.25, 1.5, 1.75, 2.0 for difficulties 1-5).

**Rationale**: The spec assumes `DifficultyMultiplier` is configurable via `GameConfiguration.ScoringSystem` (SPEC-001). The existing `ScoringSystem` enumeration has `Standard` and `ProgressiveBonus`. For `Standard`, a linear multiplier based on difficulty is the simplest reasonable default. The actual multiplier formula should be defined in `GameConfiguration` or a strategy, but for this SPEC we use the simple linear mapping.

**Alternatives considered**:
- Fixed multiplier per difficulty level: Same as chosen, just different values.
- Percentage-based (difficulty × 10% bonus): Rejected — less intuitive than direct multiplier.

### R-006: Answer Validation Chain Architecture

**Decision**: Implement 7 validation rules as `IBusinessRule` implementations called sequentially within `Game.SubmitAnswer()`, each returning `Result.Failure` with specific error code on violation.

**Rationale**: The spec defines the chain: `ValidatePlayer→ValidateGame→ValidateRound→ValidateQuestion→ValidateTime→ValidateIdempotency→EvaluateAnswer→CalculateResult` (FR-002). Following the existing pattern in `Game.cs` where rules are checked via `new XRule(...).IsBroken()` returning `Result.Failure`, each validation step is a separate `IBusinessRule`. The domain behavior `Game.SubmitAnswer()` orchestrates the chain; the Application handler just calls it.

**Alternatives considered**:
- Pipeline behavior for validation: Rejected — validation is domain-level, not cross-cutting; `ValidationBehavior` handles API-level validation only.
- Chain of Responsibility pattern: Rejected — over-engineering; sequential if-checks in domain behavior are clear and testable.

### R-007: Answer Immutability Enforcement

**Decision**: `Answer` entity exposes no public setters after construction; `Status` transitions only via internal methods (`Submit()`, `Evaluate()`, `Expire()`); `Correct`, `Points`, `ElapsedTime` set once during `Evaluate()` and never mutated.

**Rationale**: FR-017 requires immutability post-`EVALUATED`/`EXPIRED`. The `Answer` entity constructor sets all fields; internal transition methods validate pre-conditions before mutating. No `Update` or `SetCorrect` methods exist. EF Core can set fields via backing fields or `HasConversion` without exposing public setters.

**Alternatives considered**:
- Immutable record type: Rejected — EF Core requires mutable entities for materialization; backing fields provide the same protection.
- `IReadOnly` interface exposure: Rejected — adds complexity; internal methods are sufficient.

### R-008: Audit Trail for Answer Submissions

**Decision**: Append-only `AuditLog` table with `CorrelationId`, `GameId`, `RoundId`, `QuestionId`, `PlayerId`, `AnswerOptionId`, `Correct`, `Points`, `ElapsedTime`, `Status`, `FromStatus`, `ToStatus`, `Timestamp`, `Duration`.

**Rationale**: FR-016 requires audit for each `SubmitAnswer`. Following the pattern from 005-round-engine (`RoundEngineAudit`), a dedicated audit record is created for each submission attempt (success or failure). The `CorrelationId` links to OTel trace. Append-only ensures audit integrity.

**Alternatives considered**:
- Domain events only: Rejected — events are in-process and may not capture all failure cases.
- Structured logging only: Rejected — logs can be rotated; audit table provides permanent record.

## Resolved NEEDS CLARIFICATION

None — all aspects of the spec are resolvable from existing patterns and constitution.

## References

- Constitution §I (Domain First), §V (Authoritative Server Truth), §D (Scoring via Ledger), §F (Concurrency & Idempotency), §I (Validation/Errors/Audit)
- SPEC-005 (Round Engine) — `GameRound.StartedAt`, `GameRound.TimeLimit`, `GameRound.Status`
- SPEC-001 (Game Configuration) — `GameConfiguration.PointsPerRound`, `ScoringSystem`
- SPEC-003 (Question Bank) — `Question.AnswerOptions`, `AnswerOption.IsCorrect`
- Existing codebase: `Game.cs`, `GameRound.cs`, `GamePlayer.cs`, `SubmitAnswer.cs` (placeholder)
