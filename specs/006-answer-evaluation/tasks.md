# Tasks: Answer Evaluation

**Input**: Design documents from `/specs/006-answer-evaluation/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Organization**: Tasks are grouped by user story. US1 (SubmitAnswer), US2 (Answer lifecycle), and US3 (CalculateResult) are tightly coupled — they form a single cohesive flow and are implemented together.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Foundational (Domain Entities, Enums, Rules)

**Purpose**: Create all domain primitives that US1+US2+US3 depend on. Must complete before any Application layer work.

- [x] T001 [P] Create `AnswerId` StronglyTypedId in `src/OroQuizClash.Domain/Games/AnswerId.cs`
- [x] T002 [P] Create `PointTransactionId` StronglyTypedId in `src/OroQuizClash.Domain/Games/PointTransactionId.cs`
- [x] T003 [P] Create `AnswerStatus` Enumeration (NOT_ANSWERED=1, ANSWERED=2, EVALUATED=3, EXPIRED=4) with `IsTerminal` and `IsInternal` properties in `src/OroQuizClash.Domain/Games/Enumerations/AnswerStatus.cs`
- [x] T004 [P] Create `PointTransactionType` Enumeration (ANSWER_CORRECT=1, ANSWER_INCORRECT=2, ROUND_BONUS=3, LEVEL_BONUS=4) in `src/OroQuizClash.Domain/Games/Enumerations/PointTransactionType.cs`
- [x] T005 [P] Create `Answer` Entity with internal Submit/Evaluate/Expire methods, backing fields, and immutability enforcement per data-model.md in `src/OroQuizClash.Domain/Games/Answer.cs`
- [x] T006 [P] Create `PointTransaction` Entity (append-only, no Update/Delete) per data-model.md in `src/OroQuizClash.Domain/Games/PointTransaction.cs`
- [x] T007 [P] Create `AnswerResult` ValueObject (AnswerId, Correct, Points, ElapsedTime, Status) in `src/OroQuizClash.Domain/Games/ValueObjects/AnswerResult.cs`
- [x] T008 [P] Add Answer-related error codes to `GameErrors` in `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs`: `PlayerNotInGame`, `GameNotActive`, `QuestionNotActive`, `InvalidAnswer`, `AnswerTimeout`, `AnswerImmutable`
- [x] T009 [P] Create `ValidatePlayerRule` (GamePlayer.Status == IN_PROGRESS) in `src/OroQuizClash.Domain/Games/Rules/ValidatePlayerRule.cs`
- [x] T010 [P] Create `ValidateGameRule` (Game.Status IN (IN_PROGRESS, ROUND_IN_PROGRESS)) in `src/OroQuizClash.Domain/Games/Rules/ValidateGameRule.cs`
- [x] T011 [P] Create `ValidateRoundRule` (CurrentRound.Status == ROUND_IN_PROGRESS) in `src/OroQuizClash.Domain/Games/Rules/ValidateRoundRule.cs`
- [x] T012 [P] Create `ValidateTimeRule` (ServerTimestamp - StartedAt ≤ TimeLimit) in `src/OroQuizClash.Domain/Games/Rules/ValidateTimeRule.cs`
- [x] T013 [P] Create `ValidateIdempotencyRule` (PlayerId+RoundId unique) in `src/OroQuizClash.Domain/Games/Rules/ValidateIdempotencyRule.cs`
- [x] T014 [P] Create `AnswerImmutabilityRule` (Status != EVALUATED/EXPIRED) in `src/OroQuizClash.Domain/Games/Rules/AnswerImmutabilityRule.cs`
- [x] T015 [P] Create `AnswerSubmittedDomainEvent` in `src/OroQuizClash.Domain/Games/Events/AnswerSubmittedDomainEvent.cs`
- [x] T016 [P] Create `AnswerEvaluatedDomainEvent` in `src/OroQuizClash.Domain/Games/Events/AnswerEvaluatedDomainEvent.cs`

**Checkpoint**: All domain primitives exist. Application layer can now reference them.

---

## Phase 2: User Story 1+2+3 — SubmitAnswer, Answer Lifecycle, CalculateResult (Priority: P1) 🎯 MVP

**Goal**: Player submits answer → server validates 7 steps → evaluates → creates PointTransaction → returns result. Covers US1 (validation chain), US2 (Answer states), and US3 (PointTransaction ledger).

**Independent Test**: With Game IN_PROGRESS, Round ROUND_IN_PROGRESS (TimeLimit=30s), Player IN_PROGRESS, submit correct AnswerOptionId → server returns `correct=true`, `elapsedTime` server-calculated, `points` calculated, `status=EVALUATED`. Duplicate submission returns same result. Timeout returns `AnswerTimeout`. Invalid answer returns `InvalidAnswer`.

### Implementation for User Story 1+2+3

- [x] T017 [US1+US2] Extend `Game` aggregate: add `_answers` backing field, `_pointTransactions` backing field, `Answers` and `PointTransactions` read-only properties in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T018 [US1+US2+US3] Implement `Game.SubmitAnswer(Guid answerOptionId, DateTimeOffset serverTimestamp, Func<Guid, Question> questionResolver)` method with 7-step validation chain (ValidatePlayer→ValidateGame→ValidateRound→ValidateQuestion→ValidateTime→ValidateIdempotency→EvaluateAnswer→CalculateResult) in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T019 [US3] Implement `Game.CalculateResult(Answer answer)` method: creates PointTransaction with correct Type/Points based on `answer.Correct` and `Configuration.PointsPerRound × DifficultyMultiplier` in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T020 [US1+US2] Implement `Game.GetScore(Guid playerId)` method: returns `SUM(PointTransaction.Points)` for the player in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T021 [US1+US2] Implement `Game.GetAnswer(Guid playerId, GameRoundId roundId)` method: returns existing Answer for idempotency check in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T022 [US1] Rewrite `SubmitAnswer` Vertical Slice: Command (`SubmitAnswerCommand` with GameId, AnswerOptionId, RoundId?, IdempotencyKey?), Validator, Handler (loads Game via IRepository, calls `game.SubmitAnswer()`, maps Result to Response), Response DTO (`SubmitAnswerResponse` with AnswerId, Correct, Points, ElapsedTime, Status, RoundNumber, GameStatus), Endpoint (`POST /api/games/{id}/answers` with `RequireAuthorization()`) in `src/OroQuizClash.Application/Features/Games/SubmitAnswer.cs`
- [x] T023 [US1] Create `GetAnswer` Vertical Slice: Query (`GetAnswerQuery` with GameId, AnswerId), Handler (loads Answer via Specification, maps to DTO), Response DTO (`AnswerDetailResponse` per OpenAPI), Endpoint (`GET /api/games/{id}/answers/{answerId}`) in `src/OroQuizClash.Application/Features/Games/GetAnswer.cs`
- [x] T024 [US1] Create `GetPlayerScore` Vertical Slice: Query (`GetPlayerScoreQuery` with GameId, PlayerId), Handler (loads PointTransactions, calculates totals), Response DTO (`ScoreResponse` with TotalPoints, CorrectAnswers, IncorrectAnswers, TotalAnswered), Endpoint (`GET /api/games/{id}/score/{playerId}`) in `src/OroQuizClash.Application/Features/Games/GetPlayerScore.cs`
- [x] T025 [US1+US2] Create `AnswerTypeConfiguration : IEntityTypeConfiguration<Answer>` with HasKey, HasConversion for all StronglyTypedIds, RowVersion, UNIQUE index on (GameId, PlayerId, RoundId), filtered indexes per data-model.md in `src/OroQuizClash.Infrastructure/Persistence/Configurations/AnswerTypeConfiguration.cs`
- [x] T026 [US3] Create `PointTransactionTypeConfiguration : IEntityTypeConfiguration<PointTransaction>` with HasKey, HasConversion, UNIQUE index on (GameId, AnswerId), append-only (no Update/Delete config) per data-model.md in `src/OroQuizClash.Infrastructure/Persistence/Configurations/PointTransactionTypeConfiguration.cs`
- [x] T027 [US1+US2] Extend `GameTypeConfiguration` with `HasMany(g => g.Answers)` and `HasMany(g => g.PointTransactions)` composition with backing fields `_answers` and `_pointTransactions` in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GameTypeConfiguration.cs`
- [x] T028 [US1] Create `GameByIdWithAnswersSpecification` (Include Rounds+Players+Answers+PointTransactions, AsNoTracking) in `src/OroQuizClash.Infrastructure/Specifications/GameByIdWithAnswersSpecification.cs`
- [x] T029 [US1] Create `AnswerByIdSpecification` (Where GameId+AnswerId, AsNoTracking) in `src/OroQuizClash.Infrastructure/Specifications/AnswerByIdSpecification.cs`
- [x] T030 [US1] Create `AnswersByGameAndPlayerSpecification` (Where GameId+PlayerId, OrderBy RoundId, AsNoTracking) in `src/OroQuizClash.Infrastructure/Specifications/AnswersByGameAndPlayerSpecification.cs`
- [x] T031 [US3] Create `PointTransactionsByGameSpecification` (Where GameId, AsNoTracking) in `src/OroQuizClash.Infrastructure/Specifications/PointTransactionsByGameSpecification.cs`

**Checkpoint**: SubmitAnswer flow complete — Domain behavior, Application slice, Infrastructure persistence, API endpoints. Can test end-to-end.

---

## Phase 3: Polish & Cross-Cutting Concerns

**Purpose**: Audit, observability, architecture tests, quickstart validation.

- [x] T032 Add OTel structured logging for SubmitAnswer: CorrelationId, GameId, RoundId, PlayerId, AnswerOptionId, Command, Duration, Result, Correct, Points in handler
- [x] T033 Add audit trail record creation in SubmitAnswerHandler after each submission (success or failure) with all fields from FR-016
- [x] T034 Verify architecture tests pass: Domain has no refs to Infrastructure/Web, Application has no refs to concrete Infrastructure, no MediatR/MassTransit/AutoMapper
- [x] T035 Run quickstart.md validation scenarios V-001 through V-012 manually or via integration tests
- [x] T036 Update `src/OroQuizClash.Api/Program.cs` if needed for new service registrations (Answer repositories, Specifications)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Foundational)**: No dependencies — can start immediately. Creates all domain primitives.
- **Phase 2 (US1+US2+US3)**: Depends on Phase 1 completion. All tasks within can be parallelized by file (T017-T021 in Domain, T022-T024 in Application, T025-T031 in Infrastructure).
- **Phase 3 (Polish)**: Depends on Phase 2 completion.

### User Story Dependencies

- **US1 + US2 + US3 are tightly coupled** — they form the complete SubmitAnswer flow. US2 (Answer lifecycle) is the entity layer for US1. US3 (PointTransaction) is the scoring layer for US1. They cannot be implemented independently.
- All three are P1 and form the MVP.

### Parallel Opportunities

```bash
# Phase 1 — all domain primitives in parallel:
Task: "Create AnswerId StronglyTypedId"
Task: "Create PointTransactionId StronglyTypedId"
Task: "Create AnswerStatus Enumeration"
Task: "Create PointTransactionType Enumeration"
Task: "Create Answer Entity"
Task: "Create PointTransaction Entity"
Task: "Create AnswerResult ValueObject"
Task: "Create error codes"
Task: "Create ValidatePlayerRule"
Task: "Create ValidateGameRule"
Task: "Create ValidateRoundRule"
Task: "Create ValidateTimeRule"
Task: "Create ValidateIdempotencyRule"
Task: "Create AnswerImmutabilityRule"
Task: "Create domain events"

# Phase 2 — parallelize by layer:
# Domain (T017-T021) → can run together
# Application (T022-T024) → can run together (different files)
# Infrastructure (T025-T031) → can run together (different files)
```

---

## Implementation Strategy

### MVP First (Phase 1 + Phase 2)

1. Complete Phase 1: All domain primitives (entities, enums, rules, events)
2. Complete Phase 2: Full SubmitAnswer flow (domain behavior + application slice + infrastructure + API)
3. **STOP and VALIDATE**: Run quickstart.md V-001 (correct answer), V-002 (incorrect), V-003 (timeout), V-004 (idempotent)
4. Deploy/demo if ready

### Incremental Delivery

1. Phase 1 → Domain primitives ready
2. Phase 2 → SubmitAnswer complete → Test independently → Deploy/Demo (MVP!)
3. Phase 3 → Polish + audit → Deploy/Demo

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- US1+US2+US3 are combined because they form a single cohesive flow
- The existing `SubmitAnswer.cs` is a placeholder/demo — T022 rewrites it completely
- `Game.SubmitAnswer()` is the core domain behavior — T018 is the most critical task
- `PointTransaction` is append-only — no Update/Delete operations exist on the entity
- `Answer` is immutable post-EVALUATED/EXPIRED — enforced by AnswerImmutabilityRule + no public setters
