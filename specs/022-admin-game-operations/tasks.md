# Tasks: Admin Game Operations

**Input**: Design documents from `/specs/022-admin-game-operations/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [ ] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare live-operations feature scaffolding

- [X] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults, MapGameHubForwarder)
- [X] T002 Create live-operations feature directories `src/Admin/QuizArena.Admin/Components/LiveGame/` and `src/Admin/QuizArena.Admin.Client/Models/LiveGame/` and `src/Admin/QuizArena.Admin.Client/Pages/LiveGame/`
- [X] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, live view contract, operation contract, poller — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [X] T004 [P] Create live view DTOs `LiveGameView`, `QuestionView`, `AnswerView`, `LiveScore`, `GameStateView` with 10 indicators mapping in `src/Admin/QuizArena.Admin.Client/Models/LiveGame/LiveGameView.cs`
- [X] T005 [P] Create `GameRoundState`, `PlayerPresence`, `GameTimer` DTOs and `GameOperation` + `GameAuditEntry` in `src/Admin/QuizArena.Admin.Client/Models/LiveGame/LiveGameView.cs`
- [X] T006 [P] Create shared service contracts `ILiveGameService` (GetLiveGames/GetLiveGame/GetLeaderboard) in `src/Admin/QuizArena.Admin.Client/Services/ILiveGameService.cs` and `ILiveGameOperationsService` (Pause/Resume/Cancel/ForceFinish) in `src/Admin/QuizArena.Admin.Client/Services/ILiveGameOperationsService.cs`
- [X] T007 Create polling fallback `LiveGamePoller` with 3–5s PeriodicTimer + visibilityState handling in `src/Admin/QuizArena.Admin.Client/Services/LiveGamePoller.cs`
- [X] T008 Verify BFF forwarder catch-all `MapBffForwarder()` covers `/bff/games/{id}/live` + leaderboard/players and hub `MapGameHubForwarder()` covers `/hubs/game` in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`
- [X] T009 Create `LiveGameStateBadge` mapping helper for 8 states admin→domain in `src/Admin/QuizArena.Admin.Client/Models/LiveGame/LiveGameView.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Supervisar el estado vivo de una partida (Priority: P1) 🎯 MVP

**Goal**: Operador abre vista en vivo `/admin/live/{gameId}` y ve 10 indicadores operativos (Game Status, Current Round, Current Question A–D, Players/Connected/Answered/Waiting, Scores, Current Level, Game Timer) con actualización sin recarga (polling 3–5s o WebSocket via BFF hub), skeleton per-indicator, Empty/Error aislado

**Independent Test**: Crear juego Running con 2 jugadores → abrir `/admin/live/{gameId}` → verificar 10 indicadores coherentes con `GET /bff/games/{id}/live` y `leaderboard` (Status Running, Round 1, Question 4 opciones, conteos, scores, level, timer) y que se actualizan sin recarga; REWARD_MANAGER → Access Denied

### Implementation for User Story 1

- [X] T010 [P] [US1] Implement `ClientLiveGameService.GetLiveGameAsync` calling `GET /bff/games/{id}/live` (or fan-out 4 calls) via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientLiveGameService.cs`
- [X] T011 [P] [US1] Implement `ServerLiveGameService.GetLiveGameAsync` with `HttpClient http://oroclash-api` + Bearer from HttpContext and hub forwarder in `src/Admin/QuizArena.Admin/Services/ServerLiveGameService.cs`
- [X] T012 [P] [US1] Implement `ClientLiveGameService.GetLiveGamesAsync` for listado `/admin/live` (filter Running/Paused) in `src/Admin/QuizArena.Admin.Client/Services/ClientLiveGameService.cs`
- [X] T013 [P] [US1] Create `LiveGameHeader.razor` (GameStatus badge + CurrentRound/Level + GameTimer with aria-live) in `src/Admin/QuizArena.Admin/Components/LiveGame/LiveGameHeader.razor`
- [X] T014 [P] [US1] Create `LiveQuestionCard.razor` (CurrentQuestion with 4 opciones A–D, skeleton, without IsCorrect) in `src/Admin/QuizArena.Admin/Components/LiveGame/LiveQuestionCard.razor`
- [X] T015 [P] [US1] Create `LivePlayersPanel.razor` (Players/Connected/Answered/Waiting with ValidQuestionCount guard, aria-live) in `src/Admin/QuizArena.Admin/Components/LiveGame/LivePlayersPanel.razor`
- [X] T016 [P] [US1] Create `LiveScoresTable.razor` (Scores ledger reconstruction, CurrentLevel, sorted) in `src/Admin/QuizArena.Admin/Components/LiveGame/LiveScoresTable.razor`
- [X] T017 [US1] Create `LiveGameDetail.razor` page `/admin/live/{gameId}` integrating 10 indicators with polling 3–5s fallback and WebSocket via `HubConnection` to `/hubs/game` (Group game-{id}) in `src/Admin/QuizArena.Admin.Client/Pages/LiveGame/LiveGameDetail.razor`
- [X] T018 [US1] Handle LiveGame loading states (Loading skeleton per indicator, Ready, Empty 0 jugadores, Error with isolated retry) and 401 handling (stop polling, SessionExpired banner) in `src/Admin/QuizArena.Admin.Client/Pages/LiveGame/LiveGameDetail.razor`

**Checkpoint**: US1 fully functional and independently testable — 10 indicators live without full reload per spec scenarios 1–5

---

## Phase 4: User Story 2 - Controlar el ciclo de vida en ejecución con auditoría (Priority: P1)

**Goal**: Operador ejecuta 4 acciones controladas Pause (Running→Paused), Resume (Paused→Running), Cancel y Force Finish (→Finished/Cancelled) con confirmación, RowVersion/If-Match + IdempotencyKey, y auditoría append-only; transiciones inválidas 422, REWARD_MANAGER 403

**Independent Test**: Con juego Running → Pause → verificar Paused + timer congelado + audit entry; Resume → Running; Cancel/ForceFinish → terminal + audit privileged; Finished → Pause → 422; REWARD_MANAGER → 403

### Implementation for User Story 2

- [X] T019 [P] [US2] Implement `ClientLiveGameOperationsService` methods Pause/Resume/Cancel/ForceFinish calling `POST /bff/games/{id}/pause|resume|cancel|force-finish` with `If-Match` RowVersion + `X-Idempotency-Key` in `src/Admin/QuizArena.Admin.Client/Services/ClientLiveGameOperationsService.cs`
- [X] T020 [P] [US2] Implement `ServerLiveGameOperationsService` forwarding to `http://oroclash-api/api/games/{id}/*` with Bearer + IdempotencyKey in `src/Admin/QuizArena.Admin/Services/ServerLiveGameOperationsService.cs`
- [X] T021 [P] [US2] Create `LiveOperationsBar.razor` with 4 buttons (Pause/Resume/Cancel/ForceFinish) enabled by `GameStateView`, confirmation dialog, RowVersion, and IdempotencyKey generation per click in `src/Admin/QuizArena.Admin/Components/LiveGame/LiveOperationsBar.razor`
- [X] T022 [US2] Integrate `LiveOperationsBar` into `LiveGameDetail.razor` with dialog confirmation and audit success handling (append to history, no duplicate on retry) in `src/Admin/QuizArena.Admin.Client/Pages/LiveGame/LiveGameDetail.razor`
- [X] T023 [US2] Handle invalid transitions mapping 422 `InvalidGameState` without mutation or audit success in `src/Admin/QuizArena.Admin/Components/LiveGame/LiveOperationsBar.razor`
- [X] T024 [US2] Wire authorization `AdminOrGameManager` on live pages and operations; REWARD_MANAGER gets Access Denied UI + 403 on API with disabled buttons and reason in `src/Admin/QuizArena.Admin.Client/Pages/LiveGame/LiveGameDetail.razor`

**Checkpoint**: US1 and US2 both independently functional — supervision + 4 controlled actions with audit

---

## Phase 5: User Story 3 - Mantener coherencia operativa y manejo de edge cases (Priority: P2)

**Goal**: Vista en vivo mantiene coherencia `Players Answered + Waiting == Connected`, `Scores` ledger + `Current Level` derivado, `Game Timer` sincronizado con servidor (congelado en Paused), reconexión automática sin duplicar auditoría, y concurrencia `RowVersion` sin doble auditoría

**Independent Test**: Recargar vista con Connected 3/Answered 2/Waiting 1 → mismos conteos; desconexión WebSocket 5s → reconexión y re-sync sin duplicar audit; dos operadores Pause simultáneo → uno 200, otro 409 + sin segunda auditoría; Finished → timer 0 y acciones deshabilitadas

### Implementation for User Story 3

- [X] T025 [P] [US3] Verify coherence: `LiveGameView` `PlayersAnswered + PlayersWaiting == PlayersConnected` server-side and `Scores` ledger reconstruction vs `GET /bff/games/{id}/leaderboard` in `src/Admin/QuizArena.Admin.Client/Services/ClientLiveGameService.cs`
- [X] T026 [US3] Implement `Game Timer` local decrement 1s + re-sync 3–5s with server `remainingSeconds` and freeze logic for `Paused` in `src/Admin/QuizArena.Admin/Components/LiveGame/LiveGameHeader.razor`
- [X] T027 [US3] Implement reconnection logic for `HubConnection` (auto-reconnect) and polling fallback with visibilityState pause + SessionExpired handling in `src/Admin/QuizArena.Admin.Client/Pages/LiveGame/LiveGameDetail.razor`
- [X] T028 [US3] Handle concurrency: `RowVersion` If-Match + `IdempotencyKey` idempotent replay (200 without mutation/audit duplicate) in `src/Admin/QuizArena.Admin/Services/ServerLiveGameOperationsService.cs`
- [X] T029 [US3] Add a11y and responsive polish for live view (focus visible, aria-live for scores/timer, 375–1536 no scroll, 44px targets) in `src/Admin/QuizArena.Admin/Components/LiveGame/LiveGameDetail.razor`

**Checkpoint**: All user stories independently functional — live view coherent and resilient

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, concurrency, audit, and validation per quickstart.md

- [X] T030 [P] Run Design System token gate `node design-system/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/LiveGame/*`
- [X] T031 [P] Add/extend `LiveGameViewTests` (10 indicators + coherence ledger + timer sync) in `tests/QuizArena.Admin.Tests/LiveGameViewTests.cs`
- [X] T032 [P] Add/extend `LiveOperationsTests` (4 actions, guards, concurrency 409, idempotency, audit, auth 403) in `tests/QuizArena.Admin.Tests/LiveOperationsTests.cs`
- [X] T033 Verify `DesignSystemNoDirectDbTests` / `AdminBffTests` still pass for live services in `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`
- [X] T034 Run quickstart.md validation scenarios V1-V4 (Aspire AppHost, ADMIN/GAME_MANAGER vs REWARD_MANAGER, 10 indicators, 4 actions, coherence, concurrency, responsive) per `specs/022-admin-game-operations/quickstart.md`
- [X] T035 [P] Cross-cutting polish: loading skeletons timing, error `ProblemDetails` without leak, `CorrelationId` logged, responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/LiveGame/*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 and US2 are both P1 — can proceed in parallel or US1 first for MVP
  - US3 (P2) depends on US1 indicators being stable (coherence needs live view)
- **Polish (Phase 6)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice (10 live indicators)
- **US2 (P1)**: After Foundational, independent of US1 but shares LiveGameDetail; can run in parallel with US1 by different developers (merge care on LiveGameDetail)
- **US3 (P2)**: After Foundational + US1 (needs live view for coherence/timer/reconnection)

### Within Each User Story

- Models/DTOs before services, services before components, components before page integration
- Validation before integration, lifecycle before audit

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files)
- T010, T011, T012, T013, T014, T015, T016 can run in parallel within US1 (different files)
- T019, T020, T021 can run in parallel within US2
- T025, T026, T027 can run in parallel within US3
- T030, T031, T032 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 services/components together:
Task: "Implement ClientLiveGameService.GetLiveGameAsync in src/Admin/QuizArena.Admin.Client/Services/ClientLiveGameService.cs"
Task: "Create LiveGameHeader.razor in src/Admin/QuizArena.Admin/Components/LiveGame/LiveGameHeader.razor"
Task: "Create LiveQuestionCard.razor in src/Admin/QuizArena.Admin/Components/LiveGame/LiveQuestionCard.razor"
Task: "Create LivePlayersPanel.razor in src/Admin/QuizArena.Admin/Components/LiveGame/LivePlayersPanel.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared DTOs + poller + BFF verify
3. Complete Phase 3: US1 (T010-T018)
4. **STOP and VALIDATE**: Login ADMIN → open live game Running → verify 10 indicators per quickstart V1
5. Deploy/demo if ready — supervision without control

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-002)
3. Add US2 → Test independently → Deploy/Demo (+ SC-003/SC-004 4 actions with audit)
4. Add US3 → Test independently → Deploy/Demo (+ SC-005/SC-008 coherence & concurrency)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T018)
- Developer B: US2 (T019-T024) — coordinate on LiveGameDetail merge
- Developer C: US3 prep (T025-T027) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 10 live indicators: Game Status, Current Round, Current Question (A–D), Players, Players Connected, Players Answered, Players Waiting, Scores (ledger), Current Level, Game Timer (server-derived, frozen in Paused)
- 4 controlled actions: Pause (Running→Paused), Resume (Paused→Running), Cancel, ForceFinish (→Finished) with RowVersion + IdempotencyKey + Confirmation + Audit append-only
- Constitution gates: Domain First, Game Lifecycle (A), Scoring via Ledger (D), Concurrency (F), BFF + OIDC + ServiceDefaults, Realtime (012)
