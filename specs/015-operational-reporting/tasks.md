# Tasks: Operational Reporting

**Input**: Design documents from `/specs/015-operational-reporting/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Included — required by constitution (Domain/Application/Api/Architecture) and quickstart.md scenarios

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify baseline reporting infrastructure before extending

- [X] T001 Verify existing `Leaderboard` query in `src/OroQuizClash.Application/Features/Games/GetLeaderboard.cs` and `LeaderboardBuilder.cs` plus `Game`/`Answer`/`PointTransaction` ledger from SPEC-007
- [X] T002 [P] Review existing `Specification` pattern in `src/OroQuizClash.Infrastructure/Specifications/GameSpecifications.cs` and `src/BuildingBlocks/BuildingBlocks.Kernel.Domain/Specifications/Specification.cs` for `Where`/`ApplyAsNoTracking`/`ApplyPaging`
- [X] T003 [P] Verify `Report.Read` policy in `src/OroQuizClash.Api/Authorization/SecurityPolicies.cs` and `OroQuizClashDbContext` `EnsureCreatedAsync` (no new tables for reporting)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core reporting building blocks that MUST complete before ANY user story — central risk if skipped, all stories depend on these

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Create reporting folder structure `src/OroQuizClash.Application/Features/Reporting/` per `plan.md`
- [X] T005 [P] Create base `ReportingSpecifications` in `src/OroQuizClash.Infrastructure/Specifications/ReportingSpecifications.cs` — helpers `GamesByCategory`/`AnswersByPeriod`/`RoundsByQuestion` with `ApplyAsNoTracking` and `from <= to` guard
- [X] T006 [P] Extend `GetLeaderboard` query in `src/OroQuizClash.Application/Features/Games/GetLeaderboard.cs` to accept `CategoryId`/`From`/`To` (add `CategoryId?`/`From`/`To` params, filter `PointTransaction` by period/category via new `Specification` before `LeaderboardBuilder.Build`)
- [X] T007 Create shared `ReportingMapper` helpers in `src/OroQuizClash.Application/Features/Reporting/ReportingMappers.cs` for `Accuracy` (`Correct/Answered*100`), `AverageResponseTime` (avg `ElapsedTime`), `Winner` (rank 1 if `FINISHED`)

**Checkpoint**: Foundation ready — reporting folder, base specs, leaderboard extended; user stories can now begin in parallel

---

## Phase 3: User Story 1 — Reporte de juego y leaderboard operativo (Priority: P1) 🎯 MVP

**Goal**: `GameReport` (Game/Start/End/Players/Rounds/Winner/TotalQuestions) y `Leaderboard` filtrado Global/Game/Category/Period sin mutar dominio

**Independent Test**: Con 2–3 juegos en estados distintos (WAITING_FOR_PLAYERS, IN_PROGRESS, FINISHED, 2–5 jugadores, 5 rondas), `GameReport` por `gameId` retorna 7 campos correctos vs ledger; `Leaderboard` con filtros `Global`/`Game`/`Category`/`Period` respeta intersección; 0 side-effects

### Tests for User Story 1

- [X] T008 [P] [US1] Domain test for `Winner` derivation in `tests/OroQuizClash.Domain.Tests/Reporting/GameReportWinnerTests.cs` — `FINISHED` → winner = rank1, `IN_PROGRESS` → null
- [X] T009 [P] [US1] Application test for `GameReport` in `tests/OroQuizClash.Application.Tests/Features/Reporting/GameReportHandlerTests.cs` — 4 jugadores/5 rondas → `TotalQuestions=5`, `Players`/`Rounds`/`Winner` correct, 0 `PointTransaction` created
- [X] T010 [P] [US1] Api contract test for `GameReport` + `Leaderboard` filters in `tests/OroQuizClash.Api.Tests/Reporting/GameReportContractTests.cs` — `GET /api/reports/games/{id}` 200, `GET /api/reports/leaderboard?gameId=...` filtered, `GET` with `gameId` inexistente → 404 without new `AuditEntry`

### Implementation for User Story 1

- [X] T011 [P] [US1] Create `GameReport` query in `src/OroQuizClash.Application/Features/Reporting/GameReport.cs` — `GetGameReportQuery(GameId)` : `IQuery<Result<GameReportResponse>>` with `GameReportResponse` (GameId/Name/Start/End/Players/Rounds/Winner/TotalQuestions), `Validator` (`GameId` required), `Handler` loads `Game` via `GameByIdWithRoundsSpecification` (`AsNoTracking`), builds `Leaderboard` for `Winner`
- [X] T012 [P] [US1] Extend `Leaderboard` query in `src/OroQuizClash.Application/Features/Games/GetLeaderboard.cs` — add `CategoryId`/`From`/`To` to `GetLeaderboardQuery`, create `LeaderboardByPeriodSpecification` and `GamesByCategorySpecification` to filter `PointTransaction`/`Game` before `LeaderboardBuilder`
- [X] T013 [US1] Create `GameReport` endpoint in `src/OroQuizClash.Application/Features/Reporting/GameReport.cs` — `MapGet("/api/reports/games/{gameId:guid}")` with `RequireAuthorization("Report.Read")`, `RequireRateLimiting("ReadLimiter")`, delegates to `ISender`
- [X] T014 [US1] Create `Leaderboard` extended endpoint in `src/OroQuizClash.Application/Features/Reporting/LeaderboardExtended.cs` or extend `GetLeaderboardEndpoint` — `MapGet("/api/reports/leaderboard")` with query `gameId`/`categoryId`/`from`/`to`, same auth

**Checkpoint**: At this point, User Story 1 is independently functional — game snapshot + leaderboard filtered, 0 side-effects, testable via `dotnet test --filter GameReport`

---

## Phase 4: User Story 2 — Reporte de jugador (Priority: P1)

**Goal**: `PlayerReport` (GamesPlayed/Won/Lost/Withdrawn, QuestionsAnswered/Correct, Accuracy, PointsEarned/Redeemed) con filtros `Game`/`Category`/`Period`

**Independent Test**: Jugador con 5 juegos (2 won,1 lost,1 withdrawn,1 in_progress → 4 played), 20 respuestas (14 correctas, 350 pts, 100 canjeados) → `PlayerReport` 10 campos exactos vs ledger, filtrado por `Game`/`Category`/`Period` limita correctamente

### Tests for User Story 2

- [X] T015 [P] [US2] Application test for `PlayerReport` in `tests/OroQuizClash.Application.Tests/Features/Reporting/PlayerReportHandlerTests.cs` — 5 juegos mixed → `GamesPlayed=4`, `GamesWon=2`, 20/14/70% + 350/100, `Game`/`Category`/`Period` filters
- [X] T016 [P] [US2] Domain test for `Accuracy` calc in `tests/OroQuizClash.Domain.Tests/Reporting/PlayerAccuracyTests.cs` — `Correct/Answered*100`, `null` if `Answered=0`

### Implementation for User Story 2

- [X] T017 [P] [US2] Create `PlayerReport` query in `src/OroQuizClash.Application/Features/Reporting/PlayerReport.cs` — `GetPlayerReportQuery(PlayerId, GameId?, CategoryId?, From?, To?)` : `IQuery<Result<PlayerReportResponse>>` with 10 fields, `Validator` (`PlayerId` required, `from` ≤ `to`), `Handler` aggregates `Game`/`Answer`/`PointTransaction`/`RewardRedemption` via `Specification` with `AsNoTracking`, no `SaveChanges`
- [X] T018 [US2] Create `PlayerReport` endpoint in `src/OroQuizClash.Application/Features/Reporting/PlayerReport.cs` — `MapGet("/api/reports/players/{playerId:guid}")` with query `gameId`/`categoryId`/`from`/`to`, `RequireAuthorization("Report.Read")`

**Checkpoint**: At this point, User Stories 1 AND 2 are functional — game and player reports independently verified

---

## Phase 5: User Story 3 — Reporte de pregunta — detección de dificultad (Priority: P2)

**Goal**: `QuestionReport` (TimesPresented/Correct/Incorrect/Accuracy/AverageResponseTime) por `questionId` con filtros `Game`/`Category`/`Period`, detecta fácil/difícil

**Independent Test**: Pregunta A 100 presentaciones/80 aciertos/4.2s + B 100/15/12.1s → reportes 80%/15% y avg <1% error, `TimesPresented` cuenta `GameRound` aunque sin respuesta, filtrado por `Category`/`Period` excluye fuera de ventana

### Tests for User Story 3

- [X] T019 [P] [US3] Application test for `QuestionReport` in `tests/OroQuizClash.Application.Tests/Features/Reporting/QuestionReportHandlerTests.cs` — 100/80/20/80%/4.2s vs 100/15/85/15%/12.1s, `TimesPresented` via `GameRound`, `AverageResponseTime` only `Evaluated`
- [X] T020 [P] [US3] Domain test for `AverageResponseTime` in `tests/OroQuizClash.Domain.Tests/Reporting/QuestionAverageTimeTests.cs` — avg only `Evaluated`, `null` if 0

### Implementation for User Story 3

- [X] T021 [P] [US3] Create `QuestionReport` query in `src/OroQuizClash.Application/Features/Reporting/QuestionReport.cs` — `GetQuestionReportQuery(QuestionId, GameId?, CategoryId?, From?, To?)` : `IQuery<Result<QuestionReportResponse>>` with 8 fields, `Handler` counts `GameRound` for `TimesPresented`, `Answer` `Evaluated` for correct/incorrect/avg, uses `QuestionByIdSpecification` + `RoundsByQuestionSpecification` + `AnswersByQuestionSpecification`
- [X] T022 [US3] Create `QuestionReport` endpoint in `src/OroQuizClash.Application/Features/Reporting/QuestionReport.cs` — `MapGet("/api/reports/questions/{questionId:guid}")` with query `gameId`/`categoryId`/`from`/`to`, `RequireAuthorization("Report.Read")`

**Checkpoint**: All question reporting independently functional — difficulty detection verified

---

## Phase 6: User Story 4 — Reporte de categoría y de recompensas (Priority: P2)

**Goal**: `CategoryReport` (Questions/Games/Players/AverageScore/AverageAccuracy) y `RewardReport` (AvailableStock/Redemptions/PointsConsumed/Pending/Delivered) con filtro `Period`

**Independent Test**: Categoría "Ciencia" 12 preguntas/10 juegos/25 jugadores → promedios <1% error; recompensa `Stock=50`/20 canjes (12 DELIVERED/8 PENDING/2000 pts) → `AvailableStock=30`/20/2000/8/12; filtro `Period` excluye fuera de rango

### Tests for User Story 4

- [X] T023 [P] [US4] Application test for `CategoryReport` in `tests/OroQuizClash.Application.Tests/Features/Reporting/CategoryReportHandlerTests.cs` — 12/10/25, avg <1% vs manual ledger
- [X] T024 [P] [US4] Application test for `RewardReport` in `tests/OroQuizClash.Application.Tests/Features/Reporting/RewardReportHandlerTests.cs` — 50/20/2000/8/12 exact, `Period` filter

### Implementation for User Story 4

- [X] T025 [P] [US4] Create `CategoryReport` query in `src/OroQuizClash.Application/Features/Reporting/CategoryReport.cs` — `GetCategoryReportQuery(CategoryId, From?, To?)` : `IQuery<Result<CategoryReportResponse>>` with 7 fields, `Handler` uses `CategoryByIdSpecification` + `GamesByCategorySpecification` + `AnswersByCategory` aggregation, `AsNoTracking`
- [X] T026 [P] [US4] Create `RewardReport` query in `src/OroQuizClash.Application/Features/Reporting/RewardReport.cs` — `GetRewardReportQuery(RewardId?, CategoryId?, From?, To?, Page?, PageSize?)` : `IQuery<Result<RewardReportResponse>>` with 7 fields, `Handler` uses `RewardByIdSpecification` + `RewardRedemptionsByPeriodSpecification` (counts `PENDING`/`DELIVERED`)
- [X] T027 [US4] Create `CategoryReport`/`RewardReport` endpoints in `src/OroQuizClash.Application/Features/Reporting/CategoryReport.cs` and `RewardReport.cs` — `MapGet("/api/reports/categories/{categoryId:guid}")` and `MapGet("/api/reports/rewards/{rewardId:guid}")` + `MapGet("/api/reports/rewards")` paginado, `RequireAuthorization("Report.Read")`

**Checkpoint**: All category/reward reporting independently functional

---

## Phase 7: User Story 5 — Filtros transversales y no-mutación (Priority: P2)

**Goal**: Todos los reportes soportan `Global`/`Game`/`Category`/`Period` combinables, 0 side-effects, `IQuery`+`Specification` verificable, `from` ≤ `to` validado

**Independent Test**: Cada reporte con combinaciones Global (sin filtros), Game, Category, Period, Game+Period, Category+Period → intersección correcta; 2 ejecuciones idénticas → `PointTransaction`/`AuditEntry` contadores no aumentan; `gameId` inexistente → vacío o `GameNotFound` sin crear datos

### Tests for User Story 5

- [X] T028 [P] [US5] Application test for filters combinables in `tests/OroQuizClash.Application.Tests/Features/Reporting/ReportingFiltersTests.cs` — `Global` vs `Game` vs `Category` vs `Period` vs `Category+Period` → intersección correcta
- [X] T029 [P] [US5] Application test for no side-effects in `tests/OroQuizClash.Application.Tests/Features/Reporting/ReportingNoMutationTests.cs` — count `PointTransaction`/`AuditEntries` before/after `IQuery` (2 runs → no increase)
- [X] T030 [P] [US5] Domain/Architecture test for CQRS in `tests/OroQuizClash.Architecture.Tests/ReportingQueryTests.cs` — asserts every reporting handler implements `IQueryHandler` and uses `Specification` when filtering, no `SaveChanges`

### Implementation for User Story 5

- [X] T031 [P] [US5] Implement `PeriodValidator` in `src/OroQuizClash.Application/Features/Reporting/Validators/PeriodValidator.cs` — `IValidator<T>` for all reporting queries, `from` ≤ `to` → `ValidationFailed`, no domain query executed
- [X] T032 [US5] Audit `GameReport` non-existent handling in `src/OroQuizClash.Application/Features/Reporting/GameReport.cs` — `gameId` inexistente → `Result.Failure(GameNotFound)` (404), otros reportes con filtros inexistentes → vacío `total=0`
- [X] T033 [US5] Ensure all reporting handlers use `ApplyAsNoTracking()` in `src/OroQuizClash.Application/Features/Reporting/*.cs` and never call `AddAsync`/`SaveChanges` — add `AsNoTracking` to every `Specification` used in reporting

**Checkpoint**: All user stories independently functional — filtering and no-mutation guaranteed

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Hardening transversal y validación end-to-end

- [X] T034 [P] Add pagination guard in `src/OroQuizClash.Application/Features/Reporting/*Report.cs` — `pageSize` max 100, `total` in response, avoid full scan on Global without `from`/`to` (optional require `from`/`to` for Global if configured)
- [X] T035 [P] Architecture test in `tests/OroQuizClash.Architecture.Tests/ReportingImmutabilityTests.cs` — asserts reporting handlers never reference `IUnitOfWork`/`SaveChanges` and `OroQuizClashDbContext` not mutated
- [X] T036 Run `dotnet build OroQuizClash.slnx` and `dotnet test OroQuizClash.slnx` — fix any regressions from reporting extension (especially `Leaderboard` extended filters)
- [X] T037 Run `specs/015-operational-reporting/quickstart.md` E2E validation — execute GameReport/PlayerReport/QuestionReport/CategoryReport/RewardReport/Leaderboard with Global/Game/Category/Period combos and verify SC-001..SC-009
- [X] T038 [P] Update `specs/015-operational-reporting/contracts/reporting-api.md` if endpoint shapes diverged — ensure `GameReport`/`PlayerReport`/`QuestionReport` etc. match actual `Response` DTOs

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 (P1) can start after Foundational — No dependencies on other stories
  - US2 (P1) can start after Foundational — No dependencies on US1, but benefits from ledger understanding
  - US3 (P2) can start after Foundational — No dependencies on US1/US2, but reuses `GameRound`/`Answer` specs
  - US4 (P2) can start after Foundational — No dependencies on US1-US3, but shares `Category`/`Reward` specs
  - US5 (P2) can start after Foundational — No dependencies, but benefits from US1-US4 for combined filter tests
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational — No dependencies
- **User Story 2 (P1)**: Can start after Foundational — No dependencies on US1, independent aggregation
- **User Story 3 (P2)**: Can start after Foundational — No dependencies on US1/US2, independent `QuestionReport`
- **User Story 4 (P2)**: Can start after Foundational — No dependencies on US1-US3, independent `Category`/`Reward`
- **User Story 5 (P2)**: Can start after Foundational — No dependencies, but validates filtering across all reports

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation
- `IQuery` + `Validator` before `Handler`
- `Specification` before `Handler` uses it
- Handler before Endpoint
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002, T003)
- All Foundational tasks marked [P] can run in parallel (T005) except T006 depends on T005, T007 independent
- Once Foundational completes, all user stories can start in parallel (if staffed)
- All tests for a user story marked [P] can run in parallel (T008 vs T009 vs T010)
- Different user stories can be worked on in parallel by different team members
- Polish tasks T034, T035, T038 can run in parallel before T036/T037

---

## Parallel Example: User Story 1

```bash
# Launch all tests for User Story 1 together:
Task: "Domain test for Winner derivation in tests/OroQuizClash.Domain.Tests/Reporting/GameReportWinnerTests.cs"
Task: "Application test for GameReport in tests/OroQuizClash.Application.Tests/Features/Reporting/GameReportHandlerTests.cs"
Task: "Api contract test for GameReport + Leaderboard filters in tests/OroQuizClash.Api.Tests/Reporting/GameReportContractTests.cs"

# Launch all models for User Story 1 together:
Task: "Create GameReport query in src/OroQuizClash.Application/Features/Reporting/GameReport.cs"
Task: "Extend Leaderboard query in src/OroQuizClash.Application/Features/Games/GetLeaderboard.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T007) — reporting folder, base specs, leaderboard extended
3. Complete Phase 3: User Story 1 (T008-T014)
4. **STOP and VALIDATE**: Test User Story 1 independently via `dotnet test --filter GameReport` — game snapshot + leaderboard filtered, 0 side-effects
5. Deploy/demo if ready — operational game reporting as MVP

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → test independently → deploy/demo (game + leaderboard)
3. Add US2 → test independently → deploy/demo (+ player)
4. Add US3 → test independently → deploy/demo (+ question)
5. Add US4 → test independently → deploy/demo (+ category/reward)
6. Add US5 → test independently → deploy/demo (+ transversal filters)
7. Each story adds value without breaking previous stories; `dotnet test` stays green throughout

### Parallel Team Strategy

With multiple developers after Foundational:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: US1 (T008-T014) — game + leaderboard
   - Developer B: US2 (T015-T018) — player
   - Developer C: US3 (T019-T022) — question
   - Developer D: US4 (T023-T027) — category/reward
   - Developer E: US5 (T028-T033) — filters
3. Stories complete and integrate independently, then team collaborates on Polish (T034-T038)

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US1=P1, US2=P1, US3=P2, US4=P2, US5=P2)
- Each user story is independently completable and testable — stop at any checkpoint to validate
- Verify tests fail before implementing (TDD where applicable)
- Commit after each task or logical group; keep `LeaderboardBuilder` as single source of truth for ranking
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence; reporting is read-only — never add `SaveChanges` in handlers
