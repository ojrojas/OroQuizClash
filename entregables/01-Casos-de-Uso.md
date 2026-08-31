# 01 — Documentación de Casos de Uso — OroQuizClash / QuizArena

> **Proyecto:** OroQuizClash — QuizArena  
> **Versión:** v1.1.0 — Specs 001–036 (`Ready for Review`)  
> **Fecha:** 31 de agosto de 2026  
> **Carpeta:** `entregables/`  
> **Fuentes:** `specs/001-036/spec.md`, `src/OroQuizClash.Domain/`, `src/OroQuizClash.Application/Features/`, `README.md:88`

---

## 1. Actores

| Actor | Descripción | Autenticación | Rol/Claim |
|-------|-------------|---------------|-----------|
| **Administrador** (`ADMIN`) | Gestiona categorías, preguntas, juegos, recompensas, jugadores, reportes y auditoría vía `QuizArena.Admin` (Blazor BFF). | OIDC `quizarena-admin` (confidential, `authorization_code` + `refresh_token` + PKCE) contra `OroIdentityServer` | `roles: admin` scope `admin` |
| **Game Manager** (`GAME_MANAGER`) | Subconjunto de ADMIN: opera juegos (lifecycle, rounds, scoring). | idem | `roles: game_manager` |
| **Reward Manager** (`REWARD_MANAGER`) | Gestiona recompensas y canjes. | idem | `roles: reward_manager` |
| **Jugador** (`PLAYER`) | Participa desde `QuizArena.Player` (Angular 22 SPA). Lobby, partida, rondas, retiro, recompensas, resultados. | OIDC `quizarena-player` (público, `authorization_code` + PKCE + `offline_access`) | `roles: player`, claim `sub` = `PlayerId` |
| **Sistema / Seeder** | Crea datos iniciales idempotentes. | interno, one-shot `OroQuizClash.Seeder` | `00000000-0000-0000-0000-000000000001` |
| **OroIdentityServer** | Única autoridad de identidad (Principio VI). Gestiona usuarios, roles, aplicaciones OIDC, `/.well-known/openid-configuration`. | — | — |

> **Gating:** `must_change_password` → redirección forzada a `Account/ChangePassword` antes de cualquier caso de uso (`src/OroQuizClash.Application/Behaviors/AuthorizationBehavior.cs:xx`, `QuizArena.Player` guard `mustChangePasswordGuard`).

---

## 2. Mapa global de casos de uso

```
                        ┌─────────────────────────────────────────┐
                        │          OroIdentityServer              │
                        │  (Podman oroidentityserver:latest)     │
                        └──────────────┬──────────────────────────┘
                                       │
              OIDC PKCE/refresh        │  OIDC confidential
        ┌──────────────────────┐       │        ┌──────────────────────┐
        │  QuizArena.Player    │◄──────┴───────►│  QuizArena.Admin     │
        │  Angular 22 SPA      │                │  Blazor Auto + YARP  │
        │  :4200               │                │  :7172 BFF /bff/**   │
        └──────────┬───────────┘                └──────────┬───────────┘
                   │ /api /hubs/game                      │ /api (via BFF)
                   └──────────────┬───────────────────────┘
                                  ▼
                        ┌───────────────────┐
                        │ OroQuizClash.Api  │
                        │  :5000  net10.0   │
                        │  SignalR GameHub  │
                        └─────────┬─────────┘
                                  │
              ┌───────────────────┼───────────────────┐
              ▼                   ▼                   ▼
         oroclash            rabbitmq              redis
         SQL Server          Outbox/RabbitMQ       cache
```

### Inventario por dominio (36 specs)

| Dominio | Specs | Casos de uso principales |
|---------|-------|--------------------------|
| **Juego (Core)** | 001, 004, 005, 006, 007 | Configuración, ciclo de vida, motor de rondas, evaluación de respuestas, scoring |
| **Jugador en partida** | 008, 009, 010, 011, 012 | Retiro, canje, consolación, multiplayer, realtime |
| **Contenido** | 002, 003 | Categorías, banco de preguntas |
| **Transversal** | 013, 014, 015, 016 | Seguridad, auditoría, reporting, design system |
| **Admin** | 017–026 | App BFF, Dashboard, Game Config, Categories, Questions, Game Ops, Rewards, Players, Reporting, Audit |
| **Player SPA** | 027–036 | App, Lobby, Game, Rounds, Answering, Scoring, Multiplayer, Results, Withdrawal, Rewards |

---

## 3. Casos de uso — Detalle

### CU-001 — Configuración de Juego (Spec 001)

- **Actor:** ADMIN / GAME_MANAGER
- **Precondición:** Categoría en estado `Active` con ≥5 preguntas válidas (`Question.Publish()` + `Category.PublishAsync()`).
- **Flujo principal:**
  1. Admin crea `Game` con `GameConfiguration` (`Name` 3–100, `CategoryId`, `MinRounds≥5`, `MaxRounds`, `InitialDifficulty 1..5`, `DifficultyStrategy`, `TimeLimit 5–300s`, `ScoringSystem`, `LossPolicy`, `WithdrawalPolicy`, `ConsolationPolicy`, `RewardRules`, `Min/MaxPlayers`, `PointsPerRound`) — `Game.Create()` (`src/OroQuizClash.Domain/Games/Game.cs:55`).
  2. Sistema valida 9 reglas (`GameNameNotEmptyRule`, `MinRoundsAtLeastFiveRule`, `RoundsRangeCoherenceRule`, `PlayersRangeCoherenceRule`, `TimeLimitPositiveRule/RangeRule`, `DifficultyStrategyRequiredRule`, `PoliciesRequiredRule`).
  3. Estado inicial `DRAFT`.
- **Flujo alterno:** `UpdateConfiguration()` solo si `!IsStarted` (`Game.cs:802`), de lo contrario `ConfigurationImmutable`.
- **API:** `POST /api/games` (`CreateGame`), `PUT /api/games/{id}`.
- **Reglas:** `ConfigurationImmutable`, `InvalidRange`, `PoliciesRequired`.

### CU-002 — Gestión de Categorías (Spec 002, 020)

- **Actor:** ADMIN
- **Entidad:** `Category` (`src/OroQuizClash.Domain/Categories/Category.cs:9`) — `KnowledgeArea`, `AcademicLevel`, `AgeRange`, `DifficultyLevel`, `CategoryTags` (≤10 tags, 2–30 chars), `PublishConfiguration`, `Status DRAFT→ACTIVE→INACTIVE→ARCHIVED`.
- **Flujo:**
  1. `Create(name KnowledgeArea AcademicLevel AgeRange Difficulty Tags)` — valida `ValidateFields` (nombre 3–100, descripción 0–500, `AgeRange` coherente).
  2. `Update()` solo en `DRAFT/INACTIVE`.
  3. `PublishAsync(IQuestionCounter)` — exige ≥5 preguntas `IsStructurallyValid` (`Question.cs:276`) y `QuestionStatus.PUBLISHED`. Cambia a `ACTIVE`.
  4. `Activate/Deactivate/Archive`.
- **API Admin:** `POST/PUT /api/categories`, `POST /api/categories/{id}/publish`, `POST /api/categories/{id}/activate|deactivate|archive`.
- **Invariante crítico:** `Game.MarkReady()` (`Game.cs:99`) rechaza si categoría no publicada o <5 válidas.

### CU-003 — Banco de Preguntas (Spec 003, 021)

- **Actor:** ADMIN
- **Entidad:** `Question` (`src/OroQuizClash.Domain/Questions/Question.cs:10`) + `AnswerOption` ×4.
- **Reglas estructurales (QST-001..005):**
  - Exactamente 4 `AnswerOption` (`QuestionMustHaveFourOptionsRule`), texto 1–500, sin duplicados case-insensitive.
  - Exactamente 1 `IsCorrect` (`ExactlyOneCorrectAnswerRule`).
  - `DifficultyLevel`, `AcademicLevel`, `AgeRange`, `CategoryId` obligatorios.
  - `Publish()` solo desde `DRAFT/INACTIVE→PUBLISHED`, verificando 4/1; `Archive()` desde cualquier no-archivado.
- **Flujo:** `Create → Update (si no ARCHIVED) → Activate/Deactivate → Publish → Archive`.
- **Disponibilidad para selección:** `IsAvailableForSelection` (`Question.cs:278`) exige `PUBLISHED` + `IsStructurallyValid`.
- **API:** `POST /api/questions`, `PUT /api/questions/{id}`, `POST /api/questions/{id}/publish|archive`.

### CU-004 — Ciclo de Vida del Juego (Spec 004, 019, 022)

- **Actor:** ADMIN / GAME_MANAGER
- **Máquina de estados:** `GameStatus` (`src/OroQuizClash.Domain/Games/Enumerations/GameStatus.cs:6`)
  ```
  DRAFT → READY → WAITING_FOR_PLAYERS → IN_PROGRESS ↔ ROUND_IN_PROGRESS ↔ ROUND_COMPLETED
        ↘ CANCELLED (desde cualquiera no-terminal)
        IN_PROGRESS/ROUND_* → FINISHED (si completed≥MinRounds) | FORCED_FINISHED
        Terminal: FINISHED, CANCELLED, FORCED_FINISHED (sin transiciones)
  ```
- **Transiciones implementadas (`Game.cs`):**
  - `MarkReady(isCategoryPublished, countValidQuestions)` (`:99`) — `DRAFT→READY`, gate ≥5 válidas.
  - `OpenLobby()` (`:128`) — `READY→WAITING_FOR_PLAYERS`.
  - `Start()` (`:159`) — `WAITING_FOR_PLAYERS→IN_PROGRESS`, exige `MinPlayers≤count≤MaxPlayers`.
  - `Cancel(reason 3–500)` (`:363`) / `ForceFinish(reason)` (`:381`).
  - `Finish()` (`:272`) — exige `completed≥MinRounds`, otorga `GameBonus`, determina ganadores, aplica `ConsolationPolicy`.
- **API:** `POST /api/games/{id}/ready`, `/lobby/open`, `/start`, `/cancel`, `/force-finish`, `/finish`.

### CU-005 — Motor de Rondas (Spec 005, 022 Live Dashboard)

- **Actor:** ADMIN (orquestador) — `POST /api/games/{id}/rounds/start`, `POST /api/games/{id}/rounds/{roundId}/complete`
- **Flujo:**
  1. `StartRound(questionId, difficulty 1..5, timeLimit 5–300)` (`Game.cs:179`): solo `IN_PROGRESS` o `ROUND_COMPLETED`; verifica `CurrentRound==null`, `MaxRounds` no alcanzado, `Question` no reutilizada (`_rounds.Any(QuestionId)`), selecciona pregunta por `IQuestionSelectionStrategy` (excluye `PreviousQuestionIds`), crea `GameRound` (`GameRound.cs:10`), cambia a `ROUND_IN_PROGRESS`, calcula `PotentialPoints = PointsPerRound × (1+(difficulty-1)×0.25)`, resetea `PlayerScore.RoundPoints` y fija `Potential`.
  2. `CompleteRound(roundId)` (`:234`): `ROUND_IN_PROGRESS→ROUND_COMPLETED`, elige `Round.Complete()`, `SecurePoints` para activos, otorga `RoundBonus` (si `ProgressiveBonus`) y `LevelBonus` (si `difficulty > previousMaxDifficulty>0`).
- **Live Game Dashboard (`/admin/live/{gameId}`):** Botones Start Round / Complete Round / Pause / Resume / Cancel / Force Finish. Realtime via SignalR `GameHub`.
- **Estrategias de dificultad:** `LinearDifficultyStrategy`, `ProgressiveDifficultyStrategy`, `AdaptiveDifficultyStrategy` (`src/OroQuizClash.Domain/Games/Strategies/`).

### CU-006 — Responder Pregunta (Spec 006, 031)

- **Actor:** PLAYER (en `ROUND_IN_PROGRESS`)
- **Precondición:** `Game.CanSubmitAnswer()` (`Game.cs:398`) y `CurrentRound != null`.
- **Flujo `SubmitAnswer(playerId, answerOptionId, serverTimestamp, questionResolver)` (`Game.cs:403`):**
  1. `ValidatePlayer` → `PlayerNotInGame`.
  2. `ValidateGame` (`GameStatus InProgress/RoundInProgress`).
  3. `ValidateRound` → `QuestionNotActive`.
  4. `ValidateQuestion` (`AnswerOptionId` pertenece a `Question` de la ronda) → `InvalidAnswer`.
  5. `ValidateTime` (`elapsed = serverTimestamp - round.StartedAt`, `TimeLimit 5–300`) → si excede, crea `Answer Expired` (`Answer.cs:74`, `Expire()`), emite `AnswerSubmittedDomainEvent`, falla con `AnswerTimeout`.
  6. `ValidateIdempotency` — si ya existe `Answer` para `(playerId, roundId)`, retorna existente (idempotencia por ronda).
  7. `EvaluateAnswer` — puntos `= isCorrect ? PointsPerRound×DifficultyMultiplier : 0`, crea `Answer` (`Answer.cs:46`, `Submit()→Evaluate()`), asigna `Correct`, `Points`, `ElapsedTime`, emite `AnswerSubmittedDomainEvent` + `AnswerEvaluatedDomainEvent`, aplica ledger (`AwardPointsInternal` o `RemovePointsInternal` con `LossPolicy`).
- **Entidad:** `Answer` (`Answer.cs:8`) — `GameId, PlayerId, RoundId, QuestionId, AnswerOptionId, Status NotAnswered→Answered→Evaluated/Expired, Correct, Points, ElapsedTime, RowVersion`.
- **API:** `POST /api/games/{id}/answers` (`SubmitAnswer`), `GET /api/games/{id}/rounds/{roundId}/question` (pregunta sin revelar correcta hasta evaluar).

### CU-007 — Sistema de Puntuación (Spec 007, 032)

- **Actor:** Sistema (automático) + ADMIN (ajustes)
- **Entidades:** `PlayerScore` VO (`src/OroQuizClash.Domain/Games/ValueObjects/PlayerScore.cs:5`) — `CurrentPoints`, `SecuredPoints`, `RoundPoints`, `PotentialPoints`, `TotalPoints`; `PointTransaction` (`PointTransaction.cs:8`) — `GameId, PlayerId, RoundId?, QuestionId?, AnswerId?, Type, Points, ResultingBalance, Reason, CreatedAt`.
- **Operaciones (`Game.cs:510`):**
  - `AwardPoints(playerId, amount, type, roundId?)` — exige `ScoringStateValidRule` (`!IsTerminal`), `!IsWithdrawn`, `amount>0`. `roundScoped` determina si suma a `RoundPoints` (transitorio) o `SecuredPoints`.
  - `RemovePoints` — usa `LossPolicy` strategy (`LoseAllPoints`, `LoseUnsecuredPoints`, etc.) vía `LossPolicyStrategyFactory`.
  - `SecurePoints(playerId)` — `RoundPoints→SecuredPoints`, emite `PointsSecuredDomainEvent`.
  - `ConsumePoints` — descuenta para canje, valida `SufficientBalanceRule`.
  - `AdjustPoints/RefundPoints` (admin, con `AdjustmentReasonRequiredRule 3–500` y `BalanceCannotGoNegativeRule`).
  - `WithdrawPlayer` — calcula deducción por `WithdrawalPolicyStrategyFactory`, marca `Withdrawn`, crea `PointTransaction WITHDRAWAL`.
  - `Finish()` — secuencia `GameBonus → Winners → Consolation` (ver CU-010).
- **Tipos:** `PointTransactionType` — `AnswerCorrect, AnswerIncorrect, RoundBonus, LevelBonus, GameBonus, Consolation, RewardRedemption, Withdrawal, Adjustment`.
- **API:** `GET /api/games/{id}/players/me` (3 métricas), `GET /api/games/{id}/leaderboard`, `POST /api/games/{id}/players/{playerId}/adjust`.

### CU-008 — Retiro del Jugador (Spec 008, 035)

- **Actor:** PLAYER
- **Flujo `WithdrawPlayer(playerId)` (`Game.cs:596`):**
  1. Rechaza si juego terminal.
  2. Valida `PlayerNotInGame`, `PlayerAlreadyWithdrawn`, `PlayerAlreadyEliminated`, `ParticipationAlreadyFinished`.
  3. Calcula deducción por `WithdrawalPolicy` (`KeepSecuredScore → Current=Secured`, `KeepCurrentScore`, `LoseAll`, etc.) vía `WithdrawalPolicyStrategyFactory`.
  4. `Player.MarkWithdrawn()` (`GamePlayer.cs:48`, `ParticipationStatus=Withdrawn`, `ExitedAt=UTC`), `Score.Deduct(deduction)`, crea `PointTransaction WITHDRAWAL` con `Reason="Withdrawal policy: {name}"`, emite `ScoreUpdatedDomainEvent` + `PlayerWithdrawnDomainEvent`.
- **Efecto:** `IsWithdrawn=true`, `IsActive=false`, `isTerminal=true` en store. Puntos finales `Current=Secured` para `KEEP_SECURED_SCORE`.
- **UI Player (035):** `WithdrawalComponent` dialog 3 métricas `Current/Secured/Potential`, 2 warnings, idempotencia `idemp-withdraw-{gameId}`, confirmación 2 pasos.
- **API:** `POST /api/games/{id}/withdraw` (`WithdrawPlayer`), `GET /api/games/{id}/players/me`.

### CU-009 — Canje de Recompensas (Spec 009, 023, 036)

- **Actor:** PLAYER (canje) + REWARD_MANAGER (catálogo, aprobación)
- **Entidades:** `Reward` (`Reward.cs:8`) — `Name, Description, PointsRequired, Stock, Status ACTIVE/INACTIVE, ExpirationDate, RowVersion`; `RewardRedemption` (`RewardRedemption.cs:10`) — `PlayerId, RewardId, GameId, Points, Status REQUESTED→APPROVED→REJECTED/DELIVERED/CANCELLED, RequestedAt, DeliveredAt, IdempotencyKey, RowVersion, Transitions`.
- **Flujo `RedeemReward` (Application `Features/Rewards/RedeemReward.cs`):**
  1. `Reward.ReserveStock(now)` (`Reward.cs:109`, `RewardAvailableRule(Status, Stock, ExpirationDate)`) — `Stock--` o `RewardUnavailable`.
  2. `Game.ConsumePoints(playerId, PointsRequired, "Reward redemption")` — verifica `SufficientBalanceRule`.
  3. `RewardRedemption.Create(playerId, rewardId, gameId, points, idempotencyKey)` — estado `REQUESTED`, transición `RequestRequested`, emite `RewardRedeemedDomainEvent` (Outbox → RabbitMQ). Ledger `PointTransaction REWARD_REDEMPTION` con `UNIQUE (GameId, AnswerId)` / `UNIQUE (PlayerId, IdempotencyKey)` en `PointTransactionTypeConfiguration.cs:32` y `RewardRedemptionTypeConfiguration.cs:32`.
  4. Idempotencia: `X-Idempotency-Key` (`idemp-redeem-{rewardId}` client, `IdempotencyRecord` server) + `RowVersion` optimista; duplicado retorna misma redención sin doble consumo.
- **UI Player (036):** Wallet `AvailablePoints` (`GET /api/rewards?gameId`), catálogo 4 métricas `Required/Status/Canjeable/Remaining Quedan 400/Te faltan 700`, detalle + diálogo 2 pasos `role="dialog"`, `X-Idempotency-Key`, confirmación `Reference`, historial `GET /api/redemptions` paginado desc.
- **API:** `GET /api/rewards?gameId`, `POST /api/rewards/{id}/redeem` (header `X-Idempotency-Key` + `X-Correlation-Id`), `GET /api/redemptions`, `GET /api/rewards/{id}`, `POST /api/rewards/{id}/approve|reject|deliver` (admin).

### CU-010 — Consolación (Spec 010)

- **Actor:** Sistema (automático en `Game.Finish()` `:272`)
- **Reglas (`ConsolationEligibilityRule`, `Game.cs:318`):** No eliminado, no ganador, `playerParticipationRounds≥MinimumParticipationRounds`, `playerAnsweredQuestions≥MinimumAnsweredQuestions`, `policy != None`.
- **Políticas:**
  - `FixedPoints` — otorga `ConsolationPoints` fijos.
  - `ParticipationBased` — `scaled = ConsolationPoints × (playerRounds/completedRounds)`.
  - `RewardBased` — `RewardRedemption.CreateAsConsolation(playerId, rewardId, gameId)` (`RewardRedemption.cs:56`, estado `APPROVED` inmediato, `points=0`, `var(--color-info)` badge).
- **Se aplica solo en `Finish()`** después de `GameBonus` y determinación de ganadores (`postBonusMaxScore`).

### CU-011 — Multiplayer (Spec 011, 033)

- **Actor:** PLAYER (join) + Sistema (lobby)
- **Flujo:** `JoinPlayer(userId, displayName)` (`Game.cs:141`) solo en `WAITING_FOR_PLAYERS`, verifica duplicado `PlayerAlreadyJoined` y `GameFull`, crea `GamePlayer` (`GamePlayer.cs:8`, `Score Zero`, `ParticipationStatus Active`, `JoinedAt`), emite `PlayerJoinedDomainEvent`. Restricción `UNIQUE (GameId, UserId)` en `GamePlayerTypeConfiguration`.
- **API:** `POST /api/games/{id}/players` (`JoinGame`), `GET /api/games`, `GET /api/games/{id}/players`.

### CU-012 — Eventos Realtime (Spec 012)

- **Actor:** Sistema → todos los clientes conectados
- **Hub:** `GameHub` (`/hubs/game`) SignalR 8 `withAutomaticReconnect [0,2000,5000,10000,30000]` (`src/OroQuizClash.Api/Hubs/GameHub.cs`).
- **Eventos:** `RoundStarted, QuestionAvailable, ScoreUpdated, RoundCompleted, GameFinished, PlayerWithdrawn`.
- **Estrategia:** Server Truth V — cliente hidrata via REST (`GET /api/games/{id}/players/me`, `/live`, `/leaderboard`) tras reconexión. RabbitMQ solo para integración (`RewardRedeemed`, `GameFinished` via Outbox), nunca source of truth.
- **Admin BFF:** YARP forward `/hubs/game` (`QuizArena.Admin/Services/BffForwarderExtensions.cs`).

### CU-013 — Seguridad (Spec 013)

- **Actor:** Sistema
- **Controles:**
  - JWT `jwks_uri` desde `/.well-known/openid-configuration` de `OroIdentityServer`. `RequireAuthorization` en todos los `IEndpoint` (`BuildingBlocks.ServiceDefaults/GlobalExceptionHandler` RFC7807).
  - `AuthorizationBehavior` (MediatR pipeline) valida `Role/Permission` por slice.
  - `IdempotencyBehavior` + `X-Idempotency-Key` / `X-Correlation-Id` echo.
  - `RowVersion` optimista en `Game, GamePlayer, Reward, RewardRedemption, IdempotencyRecord`, `Outbox`.
  - `DataProtection` keyring persistido en volumen `identity-dp-keys`.
  - Certificado OpenIddict dev persistido en `.oidc-certs/` para descifrar JWE (`AppHost.cs:86`).

### CU-014 — Auditoría (Spec 014, 026)

- **Actor:** Sistema (automático) + ADMIN (consulta)
- **Entidad:** `AuditEntry` (`src/OroQuizClash.Domain/Audit/AuditEntry.cs:5`) — `Timestamp, ActorId, ActorRoles, Action, Permission, Resource, ResourceId, GameId, PlayerId, CorrelationId, TenantId, Result, Reason, Details/Data`.
- **Pipeline:** `AuditBehavior` intercepta cada `ICommand/IQuery`, registra antes/después. `IdempotencyRecord` (`Audit/IdempotencyRecord.cs`) guarda `Key, ActorId, ResponseHash, Response`.
- **API Admin:** `GET /api/audit?gameId&playerId&action&from&to&page&pageSize` + export.
- **UI Admin (026):** Filtros, timeline, export CSV.

### CU-015 — Reporting Operacional (Spec 015, 025)

- **Actor:** ADMIN
- **Reportes:** `GetLeaderboard`, `GetScoreLedger`, `GetPlayerScore`, `GetPlayerConsolationHistory/Status`, `GetGame Reports` (Application `Features/Games/`). Métricas: `AvailablePoints` por `sub`, `Current/Secured/Potential` por jugador, consolidado por ronda.
- **UI Admin (025):** Dashboard operacional con gráficos y export.

### CU-016 — Design System (Spec 016)

- **Actor:** Diseñador / Dev
- **Artefactos:** `design-system/MASTER.md`, `tokens/design-tokens.{json,css}` (three-layer `primitive→semantic→component`, 0 hex fuera de tokens, `node .opencode/skills/design-system/scripts/validate-tokens.cjs`), `pages/*.md` + `overrides/{admin,player}.md`, `GOVERNANCE.md`, `QUALITY-GATE.md`, `docs/adr/ADR-012`.
- **Tokens:** Palette `quiz blue #2563EB + gold #F59E0B`; Admin light enterprise `#1E40AF`/Fira, Player dark cinematic `#0F172A`/Russo One+Chakra Petch. Themes `[data-theme="administration"|"player"]`. Responsive 375/768/1024/1440/1536, WCAG 2.2 AA.

### CU-017..026 — QuizArena.Admin (Specs 017–026)

| CU | Spec | Descripción corta |
|----|------|-------------------|
| CU-017 | 017 | **App BFF**: Blazor Auto, YARP `/bff/{**}→/api/{**}`, dual `Client*Service (/bff cookie)` vs `Server*Service (http://oroclash-api + BearerTokenHandler GetTokenAsync)`, `health` `/alive`. |
| CU-018 | 018 | **Dashboard**: KPIs (juegos activos, jugadores online, canjes pendientes), atajos a Game Config/Players/Rewards. |
| CU-019 | 019 | **Game Configuration**: CRUD juegos + `MarkReady/OpenLobby/Start/Cancel/Finish` con formularios validados (16 atributos). |
| CU-020 | 020 | **Categories**: CRUD + publish gate ≥5 preguntas, filtros `KnowledgeArea/AcademicLevel`. |
| CU-021 | 021 | **Question Bank**: CRUD + publish gate 4/1, filtros `CategoryId/Status/Difficulty`. |
| CU-022 | 022 | **Game Ops (Live)**: `/admin/live/{gameId}` con Start/Complete Round, Pause/Resume, Cancel/Force Finish, SignalR live. |
| CU-023 | 023 | **Rewards**: CRUD recompensas + `Stock/PointsRequired/ExpirationDate`, `Activate/Deactivate`, historial canjes `Approve/Reject/Deliver`. |
| CU-024 | 024 | **Players**: Listado `GamePlayer` con `Score` 3 métricas, `Withdrawn/Active/Winner`, `Adjust/RefundPoints`, `Eliminate`. |
| CU-025 | 025 | **Reporting**: Leaderboard, ScoreLedger, export CSV, métricas por `GameId`. |
| CU-026 | 026 | **Audit**: Timeline filtrable por `ActorId/CorrelationId/GameId/Action`, detalle `Reason/Data`. |

### CU-027..036 — QuizArena.Player SPA (Specs 027–036)

| CU | Spec | Ruta | Store | Descripción |
|----|------|------|-------|-------------|
| CU-027 | 027 | `/` | `auth` | **App shell**: Angular 22 standalone (`input/signal/computed @if/@for`), `NgRx Signals 22` (`signalStore rxMethod tapResponse sessionStorage`), `angular-auth-oidc-client 17` PKCE, `design-system/tokens` `data-theme="player"`. |
| CU-028 | 028 | `/lobby`, `/player/lobby` | `player-game.store` | **Lobby**: `GET /api/games?WAITING_FOR_PLAYERS` 8 cols paginado, `Join idemp-join-{gameId} POST /games/{id}/players`, hydrate. |
| CU-029 | 029 | `/game/:gameId` | `player-game` | **Game cinematic** 280px 1fr: `Ronda 3/10 + Timer + Question 4 Answers radiogroup + ScorePanel Potential + Leaderboard`. |
| CU-030 | 030 | `/game/:gameId` (ladder) | `player-rounds` `buildLadder 1..N` | **Rounds ladder** vertical `completed/current/upcoming` `isSecured isFinal`. |
| CU-031 | 031 | `/game/:gameId` (answers) | `answer-interaction.store` | **Answering**: selección 1/4, `SubmitAnswer`, `isTerminal canAnswer`, `ElapsedTime`, `AnswerTimeout` handling. |
| CU-032 | 032 | `/game/:gameId` (score) | `player-game` | **Scoring**: `Current/Secured/Potential` en vivo, `ScoreUpdated` SignalR. |
| CU-033 | 033 | `/game/:gameId` (multiplayer) | `GameHub` | **Multiplayer realtime**: `ScoreUpdated/RoundStarted/RoundCompleted/PlayerWithdrawn`, presence. |
| CU-034 | 034 | `/result/:gameId` | `player-game` | **Results**: `Finished/ForcedFinished/Cancelled`, ganador/consolación, `TotalPoints`. |
| CU-035 | 035 | `/game/:gameId` (withdraw) | `WithdrawalComponent` | **Withdrawal**: diálogo 3 métricas, 2 warnings, `idemp-withdraw-{gameId} POST /withdraw WITHDRAWN isTerminal`. |
| CU-036 | 036 | `/rewards`, `/rewards/history`, `/rewards/:rewardId` | `player-rewards.store` | **Rewards ⭐**: Wallet `GET /api/rewards?gameId AVAILABLE`, catalog grid `1→2→4 col` 4 métricas, detail + redeem 2 pasos `X-Idempotency-Key idemp-redeem-{rewardId} POST /redeem`, confirmation `Reference`, history paginado, consolation badge `CreateAsConsolation APPROVED points 0 info`. |

---

## 4. Diagramas (texto)

### Flujo Lobby → Game → Rewards

```
Player lobby GET /api/games → Join game → Game IN_PROGRESS → SignalR RoundStarted
 → GET /rounds/{id}/question → POST /answers → ScoreUpdated → CompleteRound
 → Leaderboard → Withdraw (optional) → Finish → Result → Rewards catalog
 → GET /rewards?gameId → Detail → Redeem (2 steps + Idempotency) → History
```

### Estados clave

- **Game:** `Game.cs:15-39` + `GameStatus.cs:6`
- **Question:** `DRAFT→ACTIVE→PUBLISHED→ARCHIVED`
- **Category:** `DRAFT→ACTIVE→INACTIVE→ARCHIVED`
- **Answer:** `NOT_ANSWERED→ANSWERED→EVALUATED|EXPIRED`
- **Reward:** `ACTIVE↔INACTIVE` (+ stock/expiration)
- **Redemption:** `REQUESTED→APPROVED→DELIVERED | REQUESTED→REJECTED|CANCELLED`

---

## 5. Referencias por archivo

- Dominio juego: `src/OroQuizClash.Domain/Games/Game.cs:15`, `GamePlayer.cs:8`, `GameRound.cs:8`, `Answer.cs:8`, `PointTransaction.cs:8`, `ValueObjects/PlayerScore.cs:5`
- Categorías/Preguntas: `Categories/Category.cs:9`, `Questions/Question.cs:10`
- Recompensas: `Rewards/Reward.cs:8`, `RewardRedemption.cs:10`, `RedemptionStatus.cs:3`
- Auditoria: `Audit/AuditEntry.cs:5`
- App: `Features/Games/*`, `Features/Rewards/RedeemReward.cs`, `Features/Categories/*`, `Features/Questions/*`
- Infra config: `Infrastructure/Persistence/Configurations/*.cs`
- Player SPA: `src/Player/QuizArena.Player/src/app/features/{lobby,game,rewards,withdrawal}/`
- Admin BFF: `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`

---

*Documento generado a partir del análisis del código fuente y specs SDD 001–036. Para detalle por spec ver `specs/<nnn>-<name>/spec.md`.*
