# Tasks: Admin Players

**Input**: Design documents from `/specs/024-admin-players/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: No TDD contract-test tasks requested in spec; validation via quickstart.md + Architecture Tests in Polish phase.

**Organization**: Tasks grouped by user story (P1, P1, P2) per strict checklist format `- [ ] Txxx [P]? [USy]? Description with file path`.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify existing Admin shell and prepare players feature scaffolding

- [x] T001 Verify Admin shell baseline from SPEC-017 in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs` (BFF YARP, OIDC, ServiceDefaults)
- [x] T002 Create players feature directories `src/Admin/QuizArena.Admin/Components/Players/` and `src/Admin/QuizArena.Admin.Client/Models/Players/` and `src/Admin/QuizArena.Admin.Client/Pages/Players/`
- [x] T003 Verify Design System tokens available at `design-system/tokens/design-tokens.css` and theme `administration` in `src/Admin/QuizArena.Admin/Components/App.razor`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Shared DTOs, service contracts, catalogs — MUST complete before any user story

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T004 [P] Create player summary DTOs `PlayerSummary`, `PlayerFilter`, `PlayerStateView` in `src/Admin/QuizArena.Admin.Client/Models/Players/Player.cs`
- [x] T005 [P] Create player detail DTOs `PlayerDetail`, `PlayerScoreSummary`, `GameHistoryEntry`, `PlayerParticipation`, `PlayerResult` in `src/Admin/QuizArena.Admin.Client/Models/Players/PlayerDetail.cs`
- [x] T006 [P] Create ledger/statistics DTOs `PointTransactionView`, `TransactionType`, `ScoreFilter`, `PlayerStatistics`, `PlayerRewardView`, `PlayerRedemptionView` in `src/Admin/QuizArena.Admin.Client/Models/Players/PlayerStatistics.cs`
- [x] T007 Create/extend shared service contracts `IPlayersService` (GetPlayers/GetPlayer/GetGames/GetParticipations/GetResult/GetScores/GetRedemptions/GetStatistics) in `src/Admin/QuizArena.Admin.Client/Services/IPlayersService.cs`
- [x] T008 Create static catalogs `PlayerCatalogs` for 4 player states, 9 game statuses, 10 transaction types, 5 redemption statuses, 6 reward types in `src/Admin/QuizArena.Admin.Client/Services/PlayerCatalogs.cs`
- [x] T009 Verify BFF forwarder catch-all `MapBffForwarder()` exists and covers `/bff/players*` in `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Consultar perfil y estado del jugador (Priority: P1) 🎯 MVP

**Goal**: ADMIN/GAME_MANAGER busca jugadores paginado y consulta perfil completo (sub, nombre, email, tenant, identificación) + estado derivado (Active/InGame/Withdrawn/Inactive) con score summary; REWARD_MANAGER limitado; PLAYER 403

**Independent Test**: Login ADMIN → /admin/players → buscar "ana" → abrir detalle → verificar perfil + estado + score summary; jugador sin historial → secciones vacías sin error; GAME_MANAGER mismo flujo; REWARD_MANAGER → limitado; no-auth → 403

**Acceptance Scenarios**: spec.md US1 scenarios 1–4

### Implementation for User Story 1

- [x] T010 [P] [US1] Extend `PlayerFilter` validation for search (0–100), pagination Page 1..N PageSize 1..100 in `src/Admin/QuizArena.Admin.Client/Models/Players/Player.cs`
- [x] T011 [P] [US1] Implement `ClientPlayersService.GetPlayersAsync`/`GetPlayerAsync` calling `GET /bff/players?search=&page=` and `GET /bff/players/{id}` via HttpClient in `src/Admin/QuizArena.Admin.Client/Services/ClientPlayersService.cs`
- [x] T012 [P] [US1] Implement `ServerPlayersService` forwarding to `http://oroclash-api/api/players*` with Bearer from HttpContext for GetPlayers/GetPlayer in `src/Admin/QuizArena.Admin/Services/ServerPlayersService.cs`
- [x] T013 [P] [US1] Create `PlayerProfileCard.razor` component (perfil: nombre, email, tenant, identificación, estado badge, score summary, skeleton, aria-live) in `src/Admin/QuizArena.Admin/Components/Players/PlayerProfileCard.razor`
- [x] T014 [P] [US1] Create `PlayerStateBadge.razor` for 4 estados Active/InGame/Withdrawn/Inactive in `src/Admin/QuizArena.Admin/Components/Players/PlayerStateBadge.razor`
- [x] T015 [US1] Create `PlayersList.razor` paginated list (`GET /bff/players?search=&page=&pageSize=`) with search, pagination, skeleton, Empty/Error in `src/Admin/QuizArena.Admin.Client/Pages/Players/PlayersList.razor`
- [x] T016 [US1] Create `PlayerDetail.razor` detail page with 6 tabs (perfil, historial, participaciones, puntuaciones, premios/canjes, estadísticas) loading by id in `src/Admin/QuizArena.Admin.Client/Pages/Players/PlayerDetail.razor`
- [x] T017 [US1] Handle 404 PlayerNotFound and 401 session expired with retry and preserve filters in `src/Admin/QuizArena.Admin.Client/Pages/Players/PlayersList.razor`
- [x] T018 [US1] Wire DI for `IPlayersService` via `AddAdminApiHttpClient` pattern in `src/Admin/QuizArena.Admin/Program.cs` and `src/Admin/QuizArena.Admin.Client/Program.cs`

**Checkpoint**: US1 fully functional and independently testable — listado y perfil/estado con autorización por rol

---

## Phase 4: User Story 2 - Consultar historial, participaciones y resultados (Priority: P1)

**Goal**: Operador consulta historial de partidas, participaciones y resultados paginados, filtrables por texto/estado/rango de fechas (<2s, sin cargar colecciones), derivados de Game/GamePlayer/Leaderboard

**Independent Test**: Jugador con 5 partidas → historial paginado → filtrar por FINISHED y fechas → participaciones con JOINED/WITHDRAWN → resultados con TotalScore/SecuredScore/Rank; ≥200 participaciones paginan sin duplicados

**Acceptance Scenarios**: spec.md US2 scenarios 1–4

### Implementation for User Story 2

- [x] T019 [P] [US2] Implement `ClientPlayersService` methods `GetPlayerGamesAsync`/`GetParticipationsAsync`/`GetResultAsync` calling `GET /bff/players/{id}/games?search=&status=&from=&to=&page=` etc. with pagination in `src/Admin/QuizArena.Admin.Client/Services/ClientPlayersService.cs`
- [x] T020 [P] [US2] Implement `ServerPlayersService` forwarding history/participations/results to `http://oroclash-api/api/players/{id}/games*` with Bearer in `src/Admin/QuizArena.Admin/Services/ServerPlayersService.cs`
- [x] T021 [P] [US2] Create `PlayerHistoryTable.razor` component (juego, categoría, status 9, fecha, rondas, score/rank, filtros, pagination, skeleton) in `src/Admin/QuizArena.Admin/Components/Players/PlayerHistoryTable.razor`
- [x] T022 [US2] Create `PlayerParticipationsTable.razor` component (participación, gameStatus, joinedAt, state) with filters by state and dates in `src/Admin/QuizArena.Admin/Components/Players/PlayerParticipationsTable.razor`
- [x] T023 [US2] Handle `GameHistoryFilter`/`ParticipationFilter` validation `From<=To`, status in catalog, pagination server-side in `src/Admin/QuizArena.Admin.Client/Models/Players/PlayerDetail.cs`
- [x] T024 [US2] Create `PlayerDetail` historial tabs integration with loading skeletons per pestaña and ProblemDetails without leak in `src/Admin/QuizArena.Admin.Client/Pages/Players/PlayerDetail.razor`
- [x] T025 [US2] Wire authorization `AdminOrGameManager` on history/participations/results; REWARD_MANAGER gets 403 on those tabs + API in `src/Admin/QuizArena.Admin.Client/Pages/Players/PlayersList.razor` and `src/Admin/QuizArena.Admin.Client/Pages/Players/PlayerDetail.razor`

**Checkpoint**: US1 and US2 both independently functional — perfil + historial/participaciones con filtros y paginación

---

## Phase 5: User Story 3 - Consultar puntuaciones, premios, canjes y estadísticas (Priority: P2)

**Goal**: Consultar puntuaciones reconstruidas desde PointTransaction ledger (10 tipos) con desglose, premios/canjes (5 estados, IsConsolation) y estadísticas agregadas server-side (TotalGames, Wins, AverageScore, AccuracyRate, BestStreak, etc.) sin cálculo en cliente

**Independent Test**: Puntuaciones → desglose 10 tipos con total = SUM ledger; premios → elegibles; canjes → 5 estados con IsConsolation:true no cuenta como normal; estadísticas → snapshot con CalculatedAt; 20 partidas → métricas correctas

**Acceptance Scenarios**: spec.md US3 scenarios 1–5

### Implementation for User Story 3

- [x] T026 [P] [US3] Implement `ClientPlayersService` methods `GetScoresAsync`/`GetRedemptionsAsync`/`GetStatisticsAsync` calling `GET /bff/players/{id}/scores?type=&from=&to=&page=` and `GET /bff/players/{id}/statistics` with pagination in `src/Admin/QuizArena.Admin.Client/Services/ClientPlayersService.cs`
- [x] T027 [P] [US3] Implement `ServerPlayersService` for scores/redemptions/statistics forwarding with Bearer in `src/Admin/QuizArena.Admin/Services/ServerPlayersService.cs`
- [x] T028 [P] [US3] Create `PlayerScoreLedger.razor` component (total ledger + desglose 10 tipos, Points, Timestamp, ReferenceId, filtros) in `src/Admin/QuizArena.Admin/Components/Players/PlayerScoreLedger.razor`
- [x] T029 [P] [US3] Create `PlayerStatisticsPanel.razor` component (TotalGames, Wins, Top3, AverageScore, AccuracyRate, BestStreak, AverageTimePerQuestion, distributions) with skeleton in `src/Admin/QuizArena.Admin/Components/Players/PlayerStatisticsPanel.razor`
- [x] T030 [US3] Enforce Consolation independent display: IsConsolation badge and not counting as normal reward in `src/Admin/QuizArena.Admin/Components/Players/PlayerScoreLedger.razor` and redemptions view
- [x] T031 [US3] Add a11y and responsive polish for players list/detail (focus visible, aria-live per-tab errors, 375–1536 no scroll, 44px targets) in `src/Admin/QuizArena.Admin/Components/Players/*` and `src/Admin/QuizArena.Admin.Client/Pages/Players/*`

**Checkpoint**: All user stories independently functional — perfil + historial + puntuaciones/premios/estadísticas con ledger y Consolation coherente

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Governance, a11y, paginación, auditoría y validación per quickstart.md

- [x] T032 [P] Run Design System token gate `node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin --strict` and fix literal violations in `src/Admin/QuizArena.Admin/Components/Players/*`
- [x] T033 [P] Add/extend `PlayerProfileTests` (perfil/estado solo lectura, búsqueda, paginación, From<=To, PlayerNotFound 404) in `tests/QuizArena.Admin.Tests/PlayerProfileTests.cs`
- [x] T034 [P] Add/extend `PlayerStatisticsTests` (ledger 10 tipos, IsConsolation, filtros, estadísticas snapshot, 403 por rol) in `tests/QuizArena.Admin.Tests/PlayerStatisticsTests.cs`
- [x] T035 Verify `DesignSystemNoDirectDbTests` / `AdminBffTests` still pass for new players services in `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`
- [x] T036 Run quickstart.md validation scenarios V1-V4 (Aspire AppHost, ADMIN/GAME_MANAGER vs REWARD_MANAGER, 9 áreas, paginación masiva, filtros) per `specs/024-admin-players/quickstart.md`
- [x] T037 [P] Cross-cutting polish: loading skeletons per pestaña, error `ProblemDetails` without leak, `CorrelationId` logged, responsive 375/768/1024/1440/1536 manual audit in `src/Admin/QuizArena.Admin/Components/Players/*`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories
- **User Stories (Phase 3+)**: All depend on Foundational
  - US1 and US2 are both P1 — can proceed in parallel or US1 first for MVP
  - US3 (P2) depends on US1 detail tabs being stable (estadísticas builds on perfil)
- **Polish (Phase 6)**: Depends on all desired user stories

### User Story Dependencies

- **US1 (P1)**: After Foundational, no other story dependency — MVP slice (perfil + listado)
- **US2 (P1)**: After Foundational, independent of US1 but shares PlayerDetail tabs; can run in parallel with US1 by different developers (merge care on PlayerDetail)
- **US3 (P2)**: After Foundational + US1 (needs perfil tabs for puntuaciones/estadísticas)

### Within Each User Story

- Models/catalogs before services, services before components, components before page integration
- Validation before integration, ledger before estadísticas
- Historial before resultados

### Parallel Opportunities

- T004, T005, T006 can run in parallel (different files)
- T010, T011, T012, T013, T014 can run in parallel within US1 (different files)
- T019, T020, T021, T022 can run in parallel within US2
- T026, T027, T028, T029 can run in parallel within US3
- T032, T033, T034 can run in parallel in Polish

---

## Parallel Example: User Story 1

```bash
# Launch US1 models/services/components together:
Task: "Extend PlayerFilter validation in src/Admin/QuizArena.Admin.Client/Models/Players/Player.cs"
Task: "Implement ClientPlayersService.GetPlayersAsync in src/Admin/QuizArena.Admin.Client/Services/ClientPlayersService.cs"
Task: "Create PlayerProfileCard.razor in src/Admin/QuizArena.Admin/Components/Players/PlayerProfileCard.razor"
Task: "Create PlayerStateBadge.razor in src/Admin/QuizArena.Admin/Components/Players/PlayerStateBadge.razor"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T009) — shared DTOs + IPlayersService + BFF verify
3. Complete Phase 3: US1 (T010-T018)
4. **STOP and VALIDATE**: Login ADMIN → buscar jugador → ver perfil/estado per quickstart V1
5. Deploy/demo if ready — perfil sin historial

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. Add US1 → Test independently → Deploy/Demo (MVP! — SC-001/SC-002)
3. Add US2 → Test independently → Deploy/Demo (+ SC-003 historial con filtros)
4. Add US3 → Test independently → Deploy/Demo (+ SC-004/006 puntuaciones/canjes/estadísticas)

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 (T010-T018)
- Developer B: US2 (T019-T025) — coordinate on PlayerDetail merge
- Developer C: US3 prep (T026-T029) after US1 stabilizes

---

## Notes

- [P] = different files, no dependencies — safe to parallelize
- [US*] = traceability to spec.md user stories
- Each user story independently completable and testable per its Independent Test
- Exact file paths per task — LLM can execute without additional context
- 9 áreas: Perfil (sub, nombre, email, tenant, identificación) + Estado (Active/InGame/Withdrawn/Inactive) + Historial (9 estados Game) + Participaciones (4 estados) + Resultados (Score/Rank/Bonuses) + Puntuaciones (10 tipos ledger) + Premios (6 tipos) + Canjes (5 estados, IsConsolation) + Estadísticas (TotalGames/Wins/Average/Accuracy/BestStreak)
- Paginación server-side `PagedResult` (Items, TotalCount, Page, PageSize) para historial (≥200), puntuaciones, canjes; filtros combinados search/status/from/to/type sin cargar colecciones
- Constitución gates: Domain First, Clean Architecture, BuildingBlocks, CQRS (GetPlayers etc.), Server Truth (ledger), OroIdentityServer (VI/H), Scoring via Ledger (D), Security (H), Observability (I), API & Frontend (J), net10.0 único
