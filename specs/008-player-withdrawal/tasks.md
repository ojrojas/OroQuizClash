# Tasks: Player Withdrawal

**Input**: Design documents from `/specs/008-player-withdrawal/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Included — constitution mandates automated tests (Domain Unit, Application, Architecture) and spec SC-001/SC-002 require policy/validation coverage.

**Organization**: Tasks grouped by user story (US1–US4 from spec.md). US1+US2 are P1 and form the MVP. Note: SPEC-007 already implemented basic withdrawal mechanics — this feature extends it with the participation status model.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Verify baseline (project exists from specs 001–007)

- [x] T001 Verify solution builds and existing tests pass: run `dotnet build` and `dotnet test tests/OroQuizClash.Domain.Tests/` from repo root (baseline: 146 domain tests passing)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Participation status model that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [x] T002 [P] Create `PlayerParticipationStatus` Enumeration (ACTIVE=1, WITHDRAWN=2, ELIMINATED=3, WINNER=4) with `IsTerminalParticipation` property (true for WITHDRAWN/ELIMINATED/WINNER) in `src/OroQuizClash.Domain/Games/Enumerations/PlayerParticipationStatus.cs`
- [x] T003 Extend `GamePlayer` Entity: replace `IsWithdrawn` backing with `ParticipationStatus` (default ACTIVE) + `ExitedAt` (DateTimeOffset?); keep `IsWithdrawn` as computed property (`ParticipationStatus == PlayerParticipationStatus.Withdrawn`); add `IsActive` computed property; update `MarkWithdrawn()` to set status+ExitedAt; add internal `MarkEliminated()` and `MarkWinner()` methods in `src/OroQuizClash.Domain/Games/GamePlayer.cs`
- [x] T004 [P] Add participation error codes to `GameErrors`: `PlayerAlreadyEliminated`, `ParticipationAlreadyFinished` in `src/OroQuizClash.Domain/Shared/Errors/GameErrors.cs`
- [x] T005 [P] Create `PlayerAlreadyEliminatedRule` (status != ELIMINATED) in `src/OroQuizClash.Domain/Games/Rules/PlayerAlreadyEliminatedRule.cs`
- [x] T006 [P] Create `ParticipationAlreadyFinishedRule` (status == ACTIVE) in `src/OroQuizClash.Domain/Games/Rules/ParticipationAlreadyFinishedRule.cs`
- [x] T007 [P] Create `PlayerWithdrawnDomainEvent` (GameId, PlayerId, RetainedPoints, PolicyName) in `src/OroQuizClash.Domain/Games/Events/PlayerWithdrawnDomainEvent.cs`
- [x] T008 [P] Create `PlayerEliminatedDomainEvent` (GameId, PlayerId, Reason) in `src/OroQuizClash.Domain/Games/Events/PlayerEliminatedDomainEvent.cs`
- [x] T009 Extend `GamePlayerTypeConfiguration`: map `ParticipationStatus` as int column with Enumeration conversion (replace IsWithdrawn column), map `ExitedAt`, remove `WithdrawnAt` mapping in `src/OroQuizClash.Infrastructure/Persistence/Configurations/GamePlayerTypeConfiguration.cs`
- [x] T010 Verify build after foundational changes: `dotnet build` must compile with 0 errors (fix any IsWithdrawn/WithdrawnAt references)

**Checkpoint**: Participation status model exists. User story implementation can begin.

---

## Phase 3: User Story 1 - Voluntary Withdrawal Preserves Eligible Points (Priority: P1) 🎯 MVP

**Goal**: ACTIVE player withdraws → policy applied → eligible points retained → status WITHDRAWN → participation ended, with auditable withdrawal record

**Independent Test**: Withdraw a player with 300 secured + 200 unsecured under KEEP_SECURED_SCORE → verify final score 300, WITHDRAWAL transaction -200, status WITHDRAWN, ExitedAt set, PlayerWithdrawnDomainEvent raised

### Tests for User Story 1

- [x] T011 [P] [US1] Domain unit tests for withdrawal with point retention: all 4 policies retain correct points, withdrawal record (transaction) created with policy in Reason, status transition, event raised, zero-points withdrawal in `tests/OroQuizClash.Domain.Tests/Games/WithdrawalRetentionTests.cs`

### Implementation for User Story 1

- [x] T012 [US1] Refactor `Game.WithdrawPlayer`: use `MarkWithdrawn()` (status-based), raise `PlayerWithdrawnDomainEvent` with retained points + policy name, keep existing policy strategy + WITHDRAWAL transaction logic in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T013 [US1] Update `WithdrawPlayer` Vertical Slice response: add `ParticipationStatus`, `WithdrawnAt` fields per contracts/withdrawal.openapi.yaml in `src/OroQuizClash.Application/Features/Games/WithdrawPlayer.cs`
- [x] T014 [US1] Run US1 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "WithdrawalRetention"`

**Checkpoint**: US1 complete — voluntary withdrawal with policy-based point retention fully functional.

---

## Phase 4: User Story 2 - Withdrawal Validation Prevents Invalid Withdrawals (Priority: P1)

**Goal**: All forbidden withdrawal attempts rejected with specific reasons: double withdrawal, post-elimination, post-game-end, post-participation-finish, player not in game

**Independent Test**: Attempt withdrawal in each forbidden state → verify each rejected with correct specific error and zero state change

### Tests for User Story 2

- [x] T015 [P] [US2] Domain unit tests for withdrawal validation: double withdrawal (PlayerAlreadyWithdrawn), eliminated player (PlayerAlreadyEliminated), terminal game (InvalidGameState), participation finished (ParticipationAlreadyFinished), unknown player (PlayerNotInGame) — each with state-unchanged assertion in `tests/OroQuizClash.Domain.Tests/Games/WithdrawalValidationTests.cs`

### Implementation for User Story 2

- [x] T016 [US2] Extend `Game.WithdrawPlayer` validation sequence per data-model.md: game terminal → player exists → status != WITHDRAWN → status != ELIMINATED → status == ACTIVE, using `PlayerAlreadyEliminatedRule` and `ParticipationAlreadyFinishedRule` in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T017 [US2] Run US2 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "WithdrawalValidation"`

**Checkpoint**: US1+US2 (all P1) complete — MVP ready.

---

## Phase 5: User Story 3 - Player Participation Status Lifecycle (Priority: P2)

**Goal**: Participation status lifecycle ACTIVE → WITHDRAWN/ELIMINATED/WINNER with protected transitions, elimination operation, and winner determination at game finish

**Independent Test**: Verify status transitions: join → ACTIVE, withdraw → WITHDRAWN, eliminate → ELIMINATED, finish with top score → WINNER; terminal states have no outgoing transitions

### Tests for User Story 3

- [x] T018 [P] [US3] Domain unit tests for participation lifecycle: initial ACTIVE on join, EliminatePlayer transitions + validation (not terminal game, player exists, only from ACTIVE), winner determination at Finish (max score, ties, withdrawn/eliminated excluded), terminal status protection in `tests/OroQuizClash.Domain.Tests/Games/ParticipationLifecycleTests.cs`

### Implementation for User Story 3

- [x] T019 [US3] Implement `Game.EliminatePlayer(Guid playerId, string reason)` — validates game non-terminal + player exists + status ACTIVE, sets ELIMINATED + ExitedAt, raises PlayerEliminatedDomainEvent in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T020 [US3] Extend `Game.Finish` with winner determination: after bonuses, set WINNER status for all ACTIVE players with maximum score (ties all win) in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T021 [US3] Create `GetPlayerParticipationStatus` Vertical Slice: Query (GameId, PlayerId) + Handler + Response (status, currentPoints, securedPoints, exitedAt per contract) + Endpoint (`GET /api/games/{id}/players/{playerId}/status`, RequireAuthorization) in `src/OroQuizClash.Application/Features/Games/GetPlayerParticipationStatus.cs`
- [x] T022 [US3] Run US3 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "ParticipationLifecycle"`

**Checkpoint**: US3 complete — full participation status lifecycle with elimination and winner determination.

---

## Phase 6: User Story 4 - Withdrawal Impact on Remaining Players (Priority: P2)

**Goal**: Withdrawn/eliminated players excluded from all scoring events; game continues normally for remaining ACTIVE players

**Independent Test**: Withdraw/eliminate players → verify they receive no round/game bonuses, no consolation, cannot answer; remaining players score normally; game completes

### Tests for User Story 4

- [x] T023 [P] [US4] Domain unit tests for exclusion + continuation: withdrawn AND eliminated players excluded from round bonuses, game bonus, consolation, securing; remaining players continue scoring; single-active-player continuation; mid-round withdrawal allowed in `tests/OroQuizClash.Domain.Tests/Games/WithdrawalImpactTests.cs`

### Implementation for User Story 4

- [x] T024 [US4] Update exclusion checks in `Game`: replace `!p.IsWithdrawn` filters with `p.IsActive` (status == ACTIVE) in CompleteRound, Finish, and scoring operations so ELIMINATED players are also excluded; update `PlayerNotWithdrawnRule` usage to cover eliminated players in `src/OroQuizClash.Domain/Games/Game.cs`
- [x] T025 [US4] Run US4 tests and verify pass: `dotnet test tests/OroQuizClash.Domain.Tests/ --filter "WithdrawalImpact"`

**Checkpoint**: All 4 user stories complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Regression fixes, architecture tests, full validation

- [x] T026 Update existing SPEC-007 tests if broken by status model (ScoringWithdrawalTests, ScoringSecureTests, ScoringGameBonusTests, ScoringConsolationTests assertions on IsWithdrawn) in `tests/OroQuizClash.Domain.Tests/Games/`
- [x] T027 [P] Create architecture tests verifying PlayerParticipationStatus is Enumeration, GamePlayer has no public setters, EliminatePlayer/WithdrawPlayer exist as domain operations in `tests/OroQuizClash.Architecture.Tests/WithdrawalDependenciesTests.cs`
- [x] T028 Run full test suite and verify zero regressions: `dotnet test tests/OroQuizClash.Domain.Tests/`, `dotnet test tests/OroQuizClash.Application.Tests/`, `dotnet test tests/OroQuizClash.Architecture.Tests/`
- [x] T029 Run quickstart.md validation scenarios 1–10 via domain/application tests
- [x] T030 Verify `dotnet build` from repo root with 0 errors

---

## Dependencies & Execution Order

### Phase Dependencies

- **Phase 1 (Setup)**: No dependencies — baseline verification
- **Phase 2 (Foundational)**: Depends on Phase 1 — BLOCKS all user stories
- **Phase 3 (US1)**: Depends on Phase 2 — 🎯 MVP
- **Phase 4 (US2)**: Depends on US1 (extends WithdrawPlayer validation)
- **Phase 5 (US3)**: Depends on Phase 2 + US1 (EliminatePlayer + winner determination build on status model)
- **Phase 6 (US4)**: Depends on US3 (exclusion covers ELIMINATED status)
- **Phase 7 (Polish)**: Depends on all user stories complete

### User Story Dependencies

```text
Phase 2 (Foundational: status model)
├── US1 (P1) — withdrawal + retention ──┬── US2 (P1) [extends validation]
│                                        └── US3 (P2) [EliminatePlayer + WINNER]
│                                             └── US4 (P2) [exclusion incl. ELIMINATED]
```

### Parallel Opportunities

- Phase 2: T002, T004–T008 marked [P] (different files)
- US1/US2/US3/US4 test tasks marked [P] can be written while other stories implement
- T027 (architecture tests) parallel with T026 (regression fixes)

---

## Parallel Example: After Phase 2

```bash
# Tests can be written in parallel across stories:
Task: "T011 [US1] WithdrawalRetentionTests.cs"
Task: "T015 [US2] WithdrawalValidationTests.cs"
Task: "T018 [US3] ParticipationLifecycleTests.cs"
Task: "T023 [US4] WithdrawalImpactTests.cs"

# Implementation is sequential (all touch Game.cs):
T012 → T016 → T019/T020 → T024
```

---

## Implementation Strategy

### MVP First (US1 + US2 — all P1)

1. Complete Phase 1: Setup (T001)
2. Complete Phase 2: Foundational (T002–T010) — CRITICAL, blocks all
3. Complete Phase 3: US1 withdrawal + retention (T011–T014)
4. Complete Phase 4: US2 validation (T015–T017)
5. **STOP and VALIDATE**: Withdrawal flow fully functional with all validation

### Incremental Delivery

1. MVP (US1+US2) → withdrawal with validation
2. Add US3 → participation lifecycle + elimination + winners
3. Add US4 → exclusion guarantees for remaining players
4. Polish → regression fixes, architecture tests, full validation

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- `Game.cs` is modified by US1, US2, US3, US4 — tasks touching it are sequential
- SPEC-007 backward compatibility: `IsWithdrawn` kept as computed property; existing ScoringWithdrawalTests must continue passing (T026 if adjustments needed)
- Commit after each phase checkpoint
