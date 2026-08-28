# Tasks: Realtime Game Events

**Input**: Design documents from `/specs/012-realtime-game-events/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Included — required by constitution (Domain/Application/Api/Architecture)

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify project baseline and existing SPEC-011 realtime infra before extending

- [X] T001 Verify existing realtime infra builds: `src/OroQuizClash.Api/Hubs/GameHub.cs`, `src/OroQuizClash.Api/Hubs/SignalRGameNotificationsBroadcaster.cs`, `src/OroQuizClash.Application/Features/Games/IGameNotificationsBroadcaster.cs` compile and `dotnet test` passes on current branch
- [X] T002 [P] Review hub wiring in `src/OroQuizClash.Api/Program.cs` — confirm `AddSignalR()`, `AddScoped<IGameNotificationsBroadcaster, SignalRGameNotificationsBroadcaster>()`, `MapHub<GameHub>("/hubs/game")` with `RequireAuthorization()` still present
- [X] T003 [P] Review domain events coverage in `src/OroQuizClash.Domain/Games/Events/` — confirm 9 mappings from research.md R3 exist: `GameStartedDomainEvent`, `PlayerJoinedDomainEvent`, `RoundStartedDomainEvent`, `AnswerSubmittedDomainEvent`, `ScoreUpdatedDomainEvent`, `AnswerEvaluatedDomainEvent`, `RoundCompletedDomainEvent`, `GameFinishedDomainEvent`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Extend the port/broadcaster contract that ALL user stories depend on — must complete before any story handlers

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 Extend port `IGameNotificationsBroadcaster` in `src/OroQuizClash.Application/Features/Games/IGameNotificationsBroadcaster.cs` with 6 new methods: `GameStartedAsync`, `RoundStartedAsync`, `QuestionPresentedAsync`, `PlayerAnsweredAsync`, `RoundCompletedAsync`, `GameFinishedAsync` (keep 4 existing methods)
- [X] T005 [P] Define `QuestionPresentedPayload` DTO in `src/OroQuizClash.Application/Features/Games/Notifications/RealtimePayloads.cs` as `{ Guid QuestionId, string Text, IReadOnlyList<QuestionOptionPayload> AnswerOptions }` where `QuestionOptionPayload` is `{ Guid Id, string Text }` — without `IsCorrect`
- [X] T006 Implement new methods in `src/OroQuizClash.Api/Hubs/SignalRGameNotificationsBroadcaster.cs` — each maps to `IHubContext<GameHub>.Clients.Group($"game-{gameId}").SendAsync("<EventName>", payload, ct)` for the 6 new events
- [X] T007 Update `src/OroQuizClash.Api/Hubs/GameHub.cs` documentation header to list all 9 events and confirm `JoinGameGroup` still validates `sub` ∈ `game.Players` or `IsOrganizer` via `GameClaims`

**Checkpoint**: Foundation ready — port + broadcaster + hub docs support all 9 events; user stories can now proceed in parallel

---

## Phase 3: User Story 1 — Flujo de ronda en vivo sin recargar (Priority: P1) 🎯 MVP

**Goal**: Distribuir `RoundStarted`, `QuestionPresented`, `RoundCompleted` a jugadores activos sin recarga, sin revelar `IsCorrect`

**Independent Test**: Con juego `IN_PROGRESS` y 2+ jugadores conectados, iniciar ronda → verificar `RoundStarted` + `QuestionPresented` (sin `isCorrect`) llegan a todos sin polling; completar ronda → `RoundCompleted` llega; estado REST coincide con lo anunciado

### Tests for User Story 1

- [X] T008 [P] [US1] Contract test for `QuestionPresented` anti-trampa in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimePayloadFilteringTests.cs` — assert `QuestionPresented` payload never contains `IsCorrect`/`correctOptionId`
- [X] T009 [P] [US1] Contract test for hub names in `tests/OroQuizClash.Api.Tests/Hubs/GameHubContractTests.cs` — assert server sends `RoundStarted`, `QuestionPresented`, `RoundCompleted` with expected JSON shapes from `contracts/realtime.payloads.yaml`
- [X] T010 [P] [US1] Handler mapping test in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimeRoundFlowBroadcastTests.cs` — `RoundStartedDomainEvent` triggers `RoundStartedAsync` + `QuestionPresentedAsync` (with question loaded), `RoundCompletedDomainEvent` triggers `RoundCompletedAsync` + `LeaderboardUpdatedAsync`

### Implementation for User Story 1

- [X] T011 [P] [US1] Create `RoundFlowBroadcastHandlers` in `src/OroQuizClash.Application/Features/Games/Notifications/RoundFlowBroadcastHandlers.cs` — `IDomainEventHandler<RoundStartedDomainEvent>` that broadcasts `RoundStartedAsync` then loads `Question` via `IRepository<Question, QuestionId>` + `QuestionByIdSpecification` and broadcasts `QuestionPresentedAsync` with filtered `{Id, Text}` options
- [X] T012 [P] [US1] Extend `LeaderboardBroadcastHandler` or create `RoundCompletedBroadcastHandler` in `src/OroQuizClash.Application/Features/Games/Notifications/GameEventBroadcastHandlers.cs` — ensure `RoundCompletedDomainEvent` broadcasts `RoundCompletedAsync` (in addition to `LeaderboardUpdatedAsync` already handled)
- [X] T013 [US1] Add `try/catch + ILogger` best-effort wrapper to handlers in `src/OroQuizClash.Application/Features/Games/Notifications/RoundFlowBroadcastHandlers.cs` — broadcast failures logged with `GameId`/`RoundId` and never propagate (FR-016)
- [X] T014 [US1] Verify `QuestionPresented` filtering by inspecting `src/OroQuizClash.Domain/Questions/AnswerOption.cs` — ensure handler projection explicitly maps only `Id` + `Text`

**Checkpoint**: US1 fully functional — round lifecycle events visible live, anti-cheat verified, independently testable

---

## Phase 4: User Story 2 — Puntuación y leaderboard en vivo (Priority: P2)

**Goal**: Distribuir `PlayerAnswered` (sin opción/correctitud), `ScoreUpdated`, `LeaderboardUpdated` en vivo tras cada evaluación

**Independent Test**: Con 2+ jugadores, respuestas simultáneas → cada `PlayerAnswered` llega sin revelar opción; evaluación → `ScoreUpdated` + `LeaderboardUpdated` snapshot que coincide con `GET /leaderboard`

### Tests for User Story 2

- [X] T015 [P] [US2] Contract test for `PlayerAnswered` anti-trampa in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimePayloadFilteringTests.cs` — assert payload never contains `AnswerOptionId`/`correct`/`points`
- [X] T016 [P] [US2] Handler mapping test in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimeScoringBroadcastTests.cs` — `AnswerSubmittedDomainEvent` → `PlayerAnsweredAsync`, `ScoreUpdatedDomainEvent` → `ScoreUpdatedAsync`, `AnswerEvaluatedDomainEvent` → `LeaderboardUpdatedAsync` (snapshot via `LeaderboardBuilder`)
- [X] T017 [P] [US2] Best-effort test in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimeScoringBroadcastTests.cs` — mock broadcaster throws → handler swallows and logs, operation not failed

### Implementation for User Story 2

- [X] T018 [P] [US2] Create `PlayerAnsweredBroadcastHandler` in `src/OroQuizClash.Application/Features/Games/Notifications/PlayerAnsweredBroadcastHandler.cs` — `IDomainEventHandler<AnswerSubmittedDomainEvent>` that broadcasts `PlayerAnsweredAsync(gameId, playerId, roundId, answeredAt)` without `AnswerOptionId`
- [X] T019 [P] [US2] Verify existing `ScoreUpdatedBroadcastHandler` in `src/OroQuizClash.Application/Features/Games/Notifications/GameEventBroadcastHandlers.cs` already handles `ScoreUpdatedDomainEvent` → `ScoreUpdatedAsync` with ledger-consistent values (no change if correct, else extend)
- [X] T020 [P] [US2] Verify existing `LeaderboardBroadcastHandler` in `src/OroQuizClash.Application/Features/Games/Notifications/GameEventBroadcastHandlers.cs` handles `AnswerEvaluatedDomainEvent` → `LeaderboardUpdatedAsync` via `LeaderboardBuilder.Build(game)` snapshot
- [X] T021 [US2] Ensure `LeaderboardUpdated` payload reuses `LeaderboardEntryResponse` shape from `src/OroQuizClash.Application/Features/Games/GetLeaderboard.cs` — no divergent DTO

**Checkpoint**: US2 functional — scoring competition visible live, anti-cheat and snapshot correctness verified

---

## Phase 5: User Story 3 — Ciclo de vida del juego en vivo (Priority: P2)

**Goal**: Distribuir `GameStarted`, `PlayerJoined`, `GameFinished` para lobby/inicio/cierre en vivo

**Independent Test**: Con juego `WAITING_FOR_PLAYERS` y clientes conectados, unir jugadores → `PlayerJoined` each; start → `GameStarted`; finish → `GameFinished` with final leaderboard; no polling needed

### Tests for User Story 3

- [X] T022 [P] [US3] Handler mapping test in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimeLifecycleBroadcastTests.cs` — `GameStartedDomainEvent` → `GameStartedAsync`, `PlayerJoinedDomainEvent` → `PlayerJoinedAsync` (already exists, verify), `GameFinishedDomainEvent` → `GameFinishedAsync` with final `LeaderboardBuilder` entries
- [X] T023 [P] [US3] Verify variant handling in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimeLifecycleBroadcastTests.cs` — `GameForcedFinishedDomainEvent` and `GameCancelledDomainEvent` also trigger `GameFinishedAsync` (if not already covered by existing `PlayerStatusBroadcastHandler`)

### Implementation for User Story 3

- [X] T024 [P] [US3] Create `GameLifecycleBroadcastHandlers` in `src/OroQuizClash.Application/Features/Games/Notifications/GameLifecycleBroadcastHandlers.cs` — handlers for `GameStartedDomainEvent` → `GameStartedAsync` and `GameFinishedDomainEvent` → `GameFinishedAsync(status, entries)`; keep existing `PlayerJoinedBroadcastHandler` for `PlayerJoinedDomainEvent`
- [X] T025 [US3] Extend `PlayerStatusBroadcastHandler` handling of `GameFinishedDomainEvent` in `src/OroQuizClash.Application/Features/Games/Notifications/GameEventBroadcastHandlers.cs` if `GameFinishedAsync` should coexist with existing `PlayerStatusChangedAsync` per-entry broadcasts — ensure both are emitted or document coexistence (GameFinished is the new event, PlayerStatusChanged remains for backward compat)

**Checkpoint**: US3 functional — full game lifecycle visible live, lobby and end-game without refresh

---

## Phase 6: User Story 4 — Recuperación tras desconexión y consistencia con la fuente de verdad (Priority: P3)

**Goal**: Garantizar que SignalR nunca es fuente de verdad: reconexión recupera estado vía REST, broadcast failures never fail operation, aislamiento por juego

**Independent Test**: Desconectar jugador mid-ronda, avanzar rondas, reconectar → re-consulta `GET /games/{id}`, `/rounds/current`, `/questions/current`, `/players/{id}/state`, `/leaderboard` recupera estado completo; jugar ronda con SignalR caído → 100% operaciones con éxito; D no-miembro no recibe eventos

### Tests for User Story 4

- [X] T026 [P] [US4] Best-effort resilience test in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimeResilienceTests.cs` — mock `IGameNotificationsBroadcaster` throws for any of the 9 events → handler logs and swallows, original command's `SaveChanges` still succeeds
- [X] T027 [P] [US4] Isolation test in `tests/OroQuizClash.Api.Tests/Hubs/GameHubIsolationTests.cs` — `JoinGameGroup` with `sub` not in `game.Players` and not organizer throws `HubException`; 20 games × mocked groups verify no cross-game delivery (unit via `IHubContext` mock)
- [X] T028 [P] [US4] Source-of-truth test in `tests/OroQuizClash.Application.Tests/Features/Games/RealtimeSourceOfTruthTests.cs` — after each broadcast handler, assert REST query (`GetLeaderboard`, `GetPlayerState`, `GetCurrentRound`) returns same data as payload (SC-008)

### Implementation for User Story 4

- [X] T029 [US4] Audit and harden all handlers in `src/OroQuizClash.Application/Features/Games/Notifications/` — ensure every `HandleAsync` has `try/catch (Exception)` → `ILogger.LogError` with `GameId`/`Event` and no rethrow; verify no handler awaits outside the try block before logging
- [X] T030 [US4] Document reconnection recovery in `src/OroQuizClash.Api/Hubs/GameHub.cs` XML comment — clarify that events are not replayed and clients must re-query REST on reconnect (FR-015/FR-019), no code change required beyond docs
- [X] T031 [US4] Verify withdrawn/eliminated filtering behavior in `src/OroQuizClash.Application/Features/Games/Notifications/GameEventBroadcastHandlers.cs` and `src/OroQuizClash.Api/Hubs/GameHub.cs` — withdrawn players remain in group but `QuestionPresented`/`RoundStarted` are logically ignored by client; server-side filtering is future optimization (R8) — add `TODO` comment or implement sub-group `game-{id}-active` if time allows

**Checkpoint**: US4 functional — resilience and consistency guarantees verified, no dependence on missed events

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, docs, and cross-story hardening

- [X] T032 [P] Update `specs/012-realtime-game-events/contracts/gamehub.md` if any payload shape diverged during implementation — ensure `realtime.payloads.yaml` matches actual `SignalRGameNotificationsBroadcaster` SendAsync calls
- [X] T033 [P] Architecture test in `tests/OroQuizClash.Architecture.Tests/RealtimeDependencyTests.cs` — assert `GameHub` does not reference `OroQuizClash.Domain` directly (only via `IRepository`/`Specification` abstractions), and `IGameNotificationsBroadcaster` lives in `Application` layer
- [X] T034 [P] Run `dotnet build OroQuizClash.slnx` and `dotnet test OroQuizClash.slnx` — fix any regressions from SPEC-011 realtime extension
- [X] T035 Run `specs/012-realtime-game-events/quickstart.md` E2E validation: connect 3 SignalR clients, execute lobby→start→rounds→finish flow, verify all 9 events, disconnect/reconnect recovery, and isolation negative test
- [X] T036 [P] Update `src/OroQuizClash.Api/Hubs/GameHub.cs` example JS snippet in XML comment to show all 9 `connection.on(...)` handlers as in `contracts/gamehub.md`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 (P1) can start immediately after Foundational — no story dependencies
  - US2 (P2) can start after Foundational — independent of US1 (different handlers, different domain events)
  - US3 (P2) can start after Foundational — independent of US1/US2
  - US4 (P3) can start after Foundational — but benefits from US1-US3 being complete for full E2E; tests for US4 can run in parallel regardless
- **Polish (Phase 7)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: No dependencies on other stories — needs only `RoundStartedDomainEvent`/`RoundCompletedDomainEvent` + `Question` repo
- **US2 (P2)**: No dependencies on US1 — needs `AnswerSubmittedDomainEvent`/`ScoreUpdatedDomainEvent`/`AnswerEvaluatedDomainEvent`; reuses `LeaderboardBuilder` already extended in SPEC-011
- **US3 (P2)**: No dependencies on US1/US2 — needs `GameStartedDomainEvent`/`PlayerJoinedDomainEvent`/`GameFinishedDomainEvent`
- **US4 (P3)**: Logically depends on US1-US3 for full E2E, but unit tests for resilience/isolation/source-of-truth can run independently after Foundational

### Within Each User Story

- Tests (if included) MUST be written and FAIL before implementation
- Port/DTO tasks before handler tasks
- Handlers before integration verification
- Best-effort wrappers before considering story complete

### Parallel Opportunities

- **Phase 1**: T002 and T003 can run in parallel
- **Phase 2**: T005 can run in parallel with T004 (different file); T006 depends on T004
- **Phase 3 (US1)**: T008, T009, T010 can all run in parallel (different test files); T011 and T012 can run in parallel (different handler files)
- **Phase 4 (US2)**: T015, T016, T017 in parallel; T018, T019, T020 in parallel
- **Phase 5 (US3)**: T022 and T023 in parallel; T024 and T025 touch different files but share handlers file — run sequentially or with care
- **Phase 6 (US4)**: T026, T027, T028 all in parallel (different test files)
- **Phase 7**: T032, T033, T034, T036 can run in parallel; T035 is E2E and should run last
- Once Foundational completes, US1, US2, US3 can be worked on by different developers in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all tests for US1 together:
Task: "Contract test for QuestionPresented anti-trampa in tests/OroQuizClash.Application.Tests/Features/Games/RealtimePayloadFilteringTests.cs"
Task: "Contract test for hub names in tests/OroQuizClash.Api.Tests/Hubs/GameHubContractTests.cs"
Task: "Handler mapping test in tests/OroQuizClash.Application.Tests/Features/Games/RealtimeRoundFlowBroadcastTests.cs"

# Launch handler implementations together:
Task: "Create RoundFlowBroadcastHandlers in src/OroQuizClash.Application/Features/Games/Notifications/RoundFlowBroadcastHandlers.cs"
Task: "Extend LeaderboardBroadcastHandler for RoundCompleted in src/OroQuizClash.Application/Features/Games/Notifications/GameEventBroadcastHandlers.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003) — verify baseline
2. Complete Phase 2: Foundational (T004-T007) — extend port/broadcaster contract
3. Complete Phase 3: US1 (T008-T014) — round flow live with anti-cheat
4. **STOP and VALIDATE**: Test US1 independently — `RoundStarted`/`QuestionPresented`/`RoundCompleted` visible without polling; `QuestionPresented` never contains `isCorrect`
5. Deploy/demo if ready — MVP delivers the core "partida viva" value

### Incremental Delivery

1. Setup + Foundational → foundation ready (port supports 9 events)
2. Add US1 → test independently → deploy/demo (MVP!)
3. Add US2 → test independently → deploy/demo (competition tension)
4. Add US3 → test independently → deploy/demo (lobby/cierre en vivo)
5. Add US4 → test independently → deploy/demo (resilience guarantees)
6. Each story adds value without breaking previous stories; `dotnet test` stays green throughout

### Parallel Team Strategy

With multiple developers after Foundational:

1. Team completes Setup + Foundational together (T001-T007)
2. Once Foundational is done:
   - Developer A: US1 (T008-T014) — round flow
   - Developer B: US2 (T015-T021) — scoring live
   - Developer C: US3 (T022-T025) — lifecycle
3. All stories merge; then team collaborates on US4 resilience (T026-T031) and Polish (T032-T036)

---

## Notes

- [P] tasks = different files, no dependencies — safe to run in parallel
- [Story] label maps task to specific user story for traceability (US1=P1, US2=P2, US3=P2, US4=P3)
- Each user story is independently completable and testable — stop at any checkpoint to validate
- Verify tests fail before implementing (TDD where applicable)
- Commit after each task or logical group; keep `IGameNotificationsBroadcaster` as single source of truth for hub method names
- Avoid: vague tasks, same-file conflicts, cross-story dependencies that break independence; hub stays broadcast-only
