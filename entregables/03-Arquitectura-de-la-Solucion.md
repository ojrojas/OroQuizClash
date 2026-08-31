# 03 — Documento de Arquitectura de la Solución — OroQuizClash / QuizArena

> **Versión:** Constitución v1.1.0 (I-VI, A-J) + SDD `Constitution → Spec → Plan → Tasks → Implementation`  
> **Specs:** 001–036 (`Ready for Review`, 036 Player Rewards)  
> **Plataforma:** `net10.0` `C#12` + Angular 22 + Blazor .NET 10 + Aspire 9  
> **Fecha:** 31-08-2026  
> **AppHost:** `OroQuizClash.AppHost/AppHost.cs` (única fuente de verdad del grafo)

---

## 1. Visión y principios arquitectónicos

| Principio (Constitución) | Aplicación en la solución |
|---------------------------|----------------------------|
| **I Domain First** | Reglas en `Domain` (`Game.ConsumePoints`, `Reward.ReserveStock`, `PlayerScore`, `IBusinessRule`) — nunca en `Application/Infra`. `Game.cs:55-813` es el corazón. |
| **II Clean Architecture** | `Api → Application → Domain ← Infrastructure` estricto (`Architecture.Tests` verifica `Domain ↛ Api/Infra/Angular`). `BuildingBlocks` aísla cross-cutting. |
| **III BuildingBlocks** | `CQRS` (`ICommand/IQuery/ISender`), `EventBus.RabbitMQ`, `Kernel.Domain/CQRS`, `ServiceDefaults` (OTel, health, `GlobalExceptionHandler` RFC7807). No `MediatR/MassTransit/AutoMapper` (reinventado con `ISender` + `EfRepository` + `Specification`). |
| **IV Vertical Slice CQRS** | `Features/{Games,Rewards,Categories,Questions}/` — cada slice contiene `Command + Validator + Handler + Response + IEndpoint` delgado (`ISender`). |
| **V Server Truth** | `AvailablePoints` solo vía `GamePlayer.Score` ledger (`PointTransactions`) + `sub` autenticado. `RabbitMQ`/`SignalR` nunca source of truth; cliente `hydrate` REST tras reconnect. |
| **VI OroIdentityServer** | Contenedor Podman `localhost/oroidentityserver:latest` (OpenIddict 8 + `postgres:identitydb` + Blazor FluentUI) — única autoridad usuarios/auth (`/.well-known/openid-configuration` → `jwks_uri`). |

**Decisiones clave (ADR-010..013, `docs/adr/`):**

- **ADR-010 `GameConfiguration`** — `GameConfiguration` como ValueObject Owned + `RewardRules` + políticas configurables (ver §2.1).
- **ADR-011 `Categories`** — `Category` con `IQuestionCounter` para `PublishAsync` ≥5 válidas; separación `Category`/`Question` en agregados distintos.
- **ADR-012 `Design System`** — Three-layer tokens `primitive→semantic→component`, gobernanza `GOVERNANCE.md`, 0 hex fuera de tokens.
- **ADR-013 `Admin BFF Communication`** — BFF via YARP, tokens server-side (`HttpOnly` cookie + refresh), dual `Client*Service (/bff)` vs `Server*Service (http://oroclash-api + BearerTokenHandler)`.

---

## 2. Arquitectura lógica — Capas y módulos

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           OroQuizClash.AppHost                          │
│  Aspire 9 — grafo distribuido (Program.cs 216 líneas, ver §6)            │
│  sqlserver/oroclash ─ postgres/identitydb ─ redis ─ rabbitmq             │
│  identity-api:5080/5086 ─ oroclash-api:5000 ─ quizarena-player:4200      │
│                              ─ quizarena-admin:7172                      │
└──────────────────────────────────────────────────────────────────────────┘
          │                 │                    │               │
          ▼                 ▼                    ▼               ▼
   OroQuizClash.Api   QuizArena.Admin   QuizArena.Player   OroIdentityServer
   net10.0 Minimal    Blazor Auto + YARP   Angular 22 SPA   Podman Container
   Clean Arch          BFF .NET 10          standalone       OpenIddict 8

Detalle backend (Modular Monolith) — OroQuizClash.slnx
┌──────────────────────────────────────────────────────────────────────────┐
│ OroQuizClash.Api                │ Minimal APIs + Hubs/GameHub            │
│  Program.cs → registra BuildingBlocks.ServiceDefaults (OTel, health,   │
│  IEndpoint auto-discovery, GlobalExceptionHandler RFC7807, JWT jwks_uri)│
├──────────────────────────────────────────────────────────────────────────┤
│ OroQuizClash.Application        │ Vertical Slices Features/              │
│  Features/Games/* (20 slices): CreateGame, JoinGame, StartGame,        │
│   StartRound, CompleteRound, SubmitAnswer, WithdrawPlayer, FinishGame...│
│  Features/Rewards/* (8): CreateReward, RedeemReward, GetRewards...      │
│  Features/Categories/, Features/Questions/, Behaviors/{Authorization,    │
│   Audit, Idempotency}, Specifications/*                                  │
├──────────────────────────────────────────────────────────────────────────┤
│ OroQuizClash.Domain             │ 6 agregados + 2 VOs + 30+ Rules        │
│  Games: Game (+ PlayerScore), GamePlayer, GameRound, Answer,           │
│         PointTransaction + Strategies (Loss/Withdrawal/Difficulty)       │
│  Categories: Category (+ AgeRange, AcademicLevel...)                    │
│  Questions: Question + AnswerOption (×4, 1 correcta)                   │
│  Rewards: Reward, RewardRedemption (+ Transitions), RedemptionStatus    │
│  Audit: AuditEntry, IdempotencyRecord                                   │
│  Authorization: Permission/Role, Shared Errors                          │
├──────────────────────────────────────────────────────────────────────────┤
│ OroQuizClash.Infrastructure     │ EF Core + Outbox                       │
│  Persistence/OroQuizClashDbContext (AppDbContextBase + dispatcher)      │
│  Configurations/* (12 IEntityTypeConfiguration), EfRepository,          │
│  SpecificationEvaluator, OutboxEntityTypeConfiguration,                 │
│  Services/*, Counters/*, Selection/*                                     │
├──────────────────────────────────────────────────────────────────────────┤
│ BuildingBlocks (7 libs)         │ Kernel.Domain, CQRS, EventBus,         │
│  EventBus.RabbitMQ, Kernel.Infrastructure, Logger, ServiceDefaults       │
├──────────────────────────────────────────────────────────────────────────┤
│ OroQuizClash.Seeder             │ Worker idempotente (10 cats ×20 Q + 10 │
│  Program.cs + SeedData.cs + Worker.cs (one-shot, EnsureCreated)       │
└──────────────────────────────────────────────────────────────────────────┘
```

### 2.1 Dominio — Agregados y VOs

**`Game` (`Domain/Games/Game.cs:15` 814 líneas) — AggregateRoot `GameId`**

- Estado: `Name`, `GameConfiguration` Owned (`CategoryId`, `Min/MaxRounds`, `InitialDifficulty`, `DifficultyStrategy`, `TimeLimit`, `ScoringSystem`, `LossPolicy`, `WithdrawalPolicy`, `ConsolationPolicy`, `RewardRules`, `Min/MaxPlayers`, `PointsPerRound`), `Status GameStatus 1..9`, `RowVersion`, timestamps `Created/Ready/Started/Finished`, `CreatedBy`.
- Colecciones internas (Field access): `_players List<GamePlayer>`, `_rounds List<GameRound>`, `_answers List<Answer>`, `_pointTransactions List<PointTransaction>`, `_consolationRedemptions List<RewardRedemption>`.
- Métodos: `Create` (9 reglas), `MarkReady(Func isCategoryPublished, Func countValid)` (gate ≥5), `OpenLobby`, `JoinPlayer`, `Start`, `StartRound(questionId,difficulty,timeLimit)` (ver CU-005), `CompleteRound`, `Finish` (GameBonus→Winners→Consolation), `Cancel/ForceFinish`, `SubmitAnswer` (7 pasos), `Award/Remove/Secure/Consume/Adjust/RefundPoints`, `WithdrawPlayer/EliminatePlayer`.

**`GamePlayer` (`GamePlayer.cs:8`) — Entity `GamePlayerId`**

- `GameId FK`, `UserId (sub)`, `JoinedAt`, `DisplayName?`, `Score PlayerScore`, `ParticipationStatus Active/Withdrawn/Eliminated/Winner`, `CurrentRoundNumber`, `ExitedAt?`, `RowVersion`. Métodos `UpdateScore`, `AdvanceToRound`, `MarkWithdrawn/Eliminated/Winner` (protegido, solo `Game` invoca).

**`PlayerScore` (`ValueObjects/PlayerScore.cs:5`) — ValueObject**

- Mapeo: `CurrentPoints` (lo que ve el jugador), `SecuredPoints` (tras `Secure()`), `RoundPoints` (transitorio de la ronda), `PotentialPoints`, `TotalPoints`. Operaciones `Award(amount,roundScoped)`, `Deduct`, `Consume`, `Secure`, `ResetRound`, `SetPotential`, `CollapseToSecured`.

**`GameRound` (`GameRound.cs:8`) + `Answer` (`Answer.cs:8`) + `PointTransaction` (`PointTransaction.cs:8`)**

- Round: `GameId`, `RoundNumber`, `Difficulty 1..5`, `QuestionId`, `TimeLimit`, `Status`, `StartedAt/CompletedAt`.
- Answer: `GameId`, `PlayerId`, `RoundId`, `QuestionId`, `AnswerOptionId`, `Status NotAnswered→Answered→Evaluated/Expired`, `Correct?`, `Points`, `ElapsedTime`, `RowVersion`.
- PointTransaction: `GameId`, `PlayerId`, `RoundId?`, `QuestionId?`, `AnswerId?`, `Type PointTransactionType`, `Points (±)`, `ResultingBalance`, `Reason`, `CreatedAt`. Índices únicos descritos en `02-ER`.

**`Category` (`Categories/Category.cs:9`) + `Question` (`Questions/Question.cs:10`)**

- Category: `Name 3-100`, `Description 0-500`, `KnowledgeArea`, `AcademicLevel`, `AgeRange`, `DifficultyLevel 1..5`, `CategoryTags (≤10, 2-30)`, `PublishConfiguration`, `Status Draft→Active→Inactive→Archived`, `RowVersion`. Métodos `Create/Update/Activate/Deactivate/PublishAsync(IQuestionCounter)/Archive`.

- Question: `Text 3-500`, `CategoryId FK`, `Difficulty`, `AcademicLevel`, `AgeRange`, `Status Draft/Active/Published/Archived`, `_answerOptions List<AnswerOption> ×4 (1 correcta)`, `RowVersion`. Reglas `QST-001..005` verificadas en `Create/Update/Publish`.

**`Reward` (`Rewards/Reward.cs:8`) + `RewardRedemption` (`Rewards/RewardRedemption.cs:10`)**

- Reward: `Name`, `Description` (nullable-safe tras fix 31-08), `PointsRequired>0`, `Stock≥0`, `Status Active/Inactive`, `ExpirationDate?`, `RowVersion`. Métodos `ReserveStock(now)` (verifica `RewardAvailableRule`), `ReleaseStock` (para rollback), `IsAvailable`.

- RewardRedemption: `PlayerId (sub)`, `RewardId`, `GameId`, `Points`, `Status Requested→Approved→Rejected/Delivered/Cancelled`, `RequestedAt/DeliveredAt`, `IdempotencyKey?`, `Transitions Owned (RedemptionTransition)` con `ActorId/At`. Métodos `Create (REQUESTED)`, `CreateAsConsolation (APPROVED, points 0, systemActor Guid.Empty)`, `Approve/Reject/Deliver/Cancel`.

---

## 3. Arquitectura de aplicación — Vertical Slices

Cada slice (`src/OroQuizClash.Application/Features/<Domain>/<Slice>.cs`) sigue:

```
Command/Query record (ICommand/IQuery) → Validator (IValidator) → Handler (ICommandHandler) → Response record → IEndpoint (MapPost/MapGet con RequireAuthorization)
```

**Ejemplos representativos:**

| Slice | Command/Query | Validación | Handler | Eventos / Transacciones |
|-------|---------------|------------|---------|-------------------------|
| `CreateReward` (`Features/Rewards/CreateReward.cs:16` 87 l) | `CreateRewardCommand(Name, Description?, PointsRequired, Stock, ExpirationDate)` | `Name 3-100`, `Points>0`, `Stock≥0` | `Reward.Create→Add→SaveChanges` | `RewardCreatedDomainEvent` → Outbox |
| `RedeemReward` | `RedeemRewardCommand(RewardId, GameId, IdempotencyKey header)` | `RewardAvailableRule` | `ReserveStock→Game.ConsumePoints→Create Redemption→ledger UNIQUE→Outbox RewardRedeemed` | `RewardRedeemedDomainEvent` |
| `JoinGame` | `JoinGameCommand(GameId, UserId)` | | `Game.JoinPlayer→Save` | `PlayerJoinedDomainEvent` → `GameHub PlayerJoined` |
| `SubmitAnswer` | `SubmitAnswerCommand(GameId, AnswerOptionId, serverTimestamp)` | 7 pasos (Player/Game/Round/Question/Time/Idempotency/Evaluate) | `Game.SubmitAnswer→Award/RemovePoints` | `AnswerSubmitted/Evaluated` → `ScoreUpdated` Hub |
| `WithdrawPlayer` | `WithdrawPlayerCommand(GameId, PlayerId)` | 5 pasos (terminal/Player/double/withdrawn/participation) | `Game.WithdrawPlayer→MarkWithdrawn→Deduct→ScoreUpdatedDomainEvent` | `PlayerWithdrawnDomainEvent` |

**Pipeline Behaviors** (`Application/Behaviors/`)

- `AuthorizationBehavior` — exige `Role/Permission` declarado en el slice (FR-013). Retorna `401/403` ProblemDetails.
- `AuditBehavior` — registra `AuditEntry` por cada `ISender.SendAsync` (actor `sub`, roles, `CorrelationId`, `ResourceId`, resultado).
- `IdempotencyBehavior` — cachea por `(Key=IdempotencyKey, ActorId=sub)` en `IdempotencyRecords`, retorna `Response` cacheada si existe (soporta `X-Idempotency-Key` en canjes, joins, withdraws).

---

## 4. Infraestructura y persistencia

**Contexto** (`OroQuizClashDbContext.cs:17`)

- `DbSet<Game/Category/Question/Reward/RewardRedemption/AuditEntry/IdempotencyRecord>` + `Outbox`.
- `OnModelCreating`: `ApplyConfigurationsFromAssembly` + `OutboxEntityTypeConfiguration`.
- SQLite fallback: `ValueConverter DateTimeOffset→DateTime UTC` + `RowVersion ValueGenerated.Never` + `BumpSqliteRowVersions()` (`Guid.NewGuid().ToByteArray()`) en `SaveChanges` (`:85`), de modo que los tests `Infrastructure` con `Testcontainers.MsSql` y fallback SQLite comparten el mismo código.

**Outbox + EventBus**

- `AppDbContextBase` intercepta `RaiseDomainEvent` y persiste `OutboxMessage` en la misma transacción que el agregado (consistencia atómica).
- Worker `RabbitMQ` (BuildingBlocks.EventBus.RabbitMQ) hace `Reliable Publish` de `RewardRedeemed`/`GameFinished` integration events. Nunca source of truth — el estado de juego siempre se re-hidrata por REST.

**RowVersion y concurrencia**

- SQL Server `IsRowVersion()` genera `rowversion` server-side. EF detecta `DbUpdateConcurrencyException` y la traduce a `409 Conflict` ProblemDetails (`type:"ConcurrencyConflict"`, `code:"ConcurrencyConflict"` mapeado en Admin `RewardCreate.razor:68`).
- SQLite simula con GUID bytes client-side.

---

## 5. Arquitectura de presentación — Frontend

### 5.1 QuizArena.Player — Angular 22 SPA (`src/Player/QuizArena.Player/`)

| Capa | Stack / Patrón |
|------|----------------|
| **Framework** | Angular 22 standalone (`input()/signal()/computed()` `@if/@for` `provideRouter` `HttpClient withFetch withInterceptors`), `provideAuth` `angular-auth-oidc-client 17` PKCE |
| **Estado** | NgRx Signals 22 `signalStore { state, computed, methods, rxMethod, tapResponse }`, persistencia `sessionStorage` para `idemp-*` (join/withdraw/redeem) |
| **Realtime** | `SignalR 8` `GameHub` `withAutomaticReconnect [0,2000,5000,10000,30000]` (`game-realtime.service.ts`), estrategia `hydrate` REST tras reconnect |
| **Routing** | `app.routes.ts` 8 rutas canónicas (`player/lobby`, `player/game/:gameId`, `player/rewards/*`, `auth/callback`, `auth/logout-callback`) + redirects legacy, guards `authGuard` + `mustChangePasswordGuard` |
| **Interceptores** | `correlation-id.interceptor` (`X-Correlation-Id`), `auth.interceptor` (`Bearer` si `secureRoutes` contiene `apiUrl`), `error.interceptor` (mapea RFC7807→ProblemDetails, 429 `Retry-After`, 401→`authorize()`) |
| **Stores** | `player-game.store` (10 props `hydrate/submitAnswer/withdraw/isTerminal/canAnswer`), `player-rounds.store` (`buildLadder 1..N`), `player-rewards.store` (036 wallet/catalog/history/redeem), `answer-interaction.store` |
| **Design** | `design-system/tokens/design-tokens.css` `data-theme="player"` (dark cinematic `#0F172A`, `Russo One + Chakra Petch`), tokens three-layer, responsive `375/768/1024/1440/1536`, WCAG 2.2 AA |
| **Auth** | `core/auth/auth.service.ts` (ver §7 logout fix), `app.config.ts` `provideAuth { authority, clientId: quizarena-player, scope: openid profile email offline_access, responseType: code, silentRenew + useRefreshToken, renewTimeBeforeTokenExpires:30 }` |
| **Build** | `ng build` (`vitest 3 + Testing Library jsdom` 8 specs rewards/*), `Dockerfile` multi-stage `node build → nginx` (publish) / `ng serve` `port 4200` (Aspire dev `AddJavaScriptApp`) |

**Corrección 31-08-2026 — Logout (`auth.service.ts`):** `logout()` intenta `logoffAndRevokeTokens()` y si falla (ej. `post_logout_redirect_uri` no registrado en IdP o CORS/cert) hace `logoffLocal()` + `sessionStorage.clear()` + fallback navegacional `window.location.href = /auth/logout-callback`. `LogoutCallbackComponent` asegura limpieza y `router.navigateByUrl('/')`. Sin esta mejora el botón quedaba "sin hacer nada" cuando el IdP rechazaba `end_session`.

### 5.2 QuizArena.Admin — Blazor .NET 10 BFF (`src/Admin/`)

| Componente | Descripción |
|------------|-------------|
| **Modelo** | `QuizArena.Admin` (host) + `QuizArena.Admin.Client` (Razor Components). `Blazor Web App Auto` (Server + WebAssembly). |
| **BFF** | YARP catch-all `/bff/{**} → /api/{**}` + `/hubs/game` forwards (`BffForwarderExtensions.cs`). Adjunta `access_token` server-side (`BearerTokenHandler GetTokenAsync("access_token")`). Tokens nunca en navegador. |
| **OIDC** | Confidential `quizarena-admin` (`authorization_code + refresh_token + PKCE`) contra `OroIdentityServer` (`Oidc__Authority=https://identity-api:5086`, `Identity__ClientSecret=quizarena-admin-oidc-secret` Aspire param). Scopes `openid profile offline_access roles admin`; `CookieOidcRefresher` / `CookieOidcServiceCollectionExtensions`. |
| **Servicios** | Dual `Client*Service (/bff cookie 401→login)` vs `Server*Service (http://oroclash-api Aspire discovery + Bearer)`. `ServerRewardsService`, `ServerLiveGameService`, etc. |
| **Rewards UI** | `RewardCreate.razor (@page "/admin/rewards/new")` + `RewardFormWrapper.razor` / `RewardForm.razor` (6 `RewardType` `Monetary/Physical/Digital/Voucher/Experience/Consolation`, `Cost 1..100000`, `Stock 0=ilimitado?`, `AvailableFrom/To`, validación `RewardForm.Validate()`). `RewardsServiceCore.CreateRewardAsync(RewardForm)` envía `ApiV2CreateRequest { name, description?, type, cost, stock, availableFrom, availableTo }` a `/bff/rewards`. |
| **Corrección 31-08-2026 — Creación recompensas** | Dominio `Reward.Create` hacía `description.Trim()` → `NullReferenceException` si `Description null` (enviada como `null` por `RewardForm.Description?`). Se corrigió a `(description ?? string.Empty).Trim()` (`Domain/Rewards/Reward.cs:43`). Aplicación `CreateRewardRequest` se hizo compatible con ambos contratos: acepta `PointsRequired?`//legacy y `Cost/AvailableTo/Type/AvailableFrom`//V2 y mapea `pointsRequired = Cost ?? PointsRequired` + `expirationDate = AvailableTo ?? ExpirationDate ?? AvailableFrom` (`Features/Rewards/CreateReward.cs:66`). Cliente `RewardsServiceCore.CreateRewardAsync` ahora deserializa respuesta robusta: intenta V2 y fallback a legado por `ReadAsStringAsync` + `GetFromJson` si el Api aún responde con shape legado (sin `RowVersion`) para evitar excepción en UI (`Services/RewardsServiceCore.cs:136`). |
| **Live Dashboard** | `/admin/live/{gameId}` — panel realtime con `Start/Complete Round`, `Pause/Resume`, `Cancel/ForceFinish`, suscripción `GameHub`. |
| **Health** | `/health`, `/alive` (ETags). |

---

## 6. Orquestación y despliegue — Aspire

**`AppHost.cs` (216 líneas) — grafo declarativo**

```csharp
sqlServer.AddDatabase("oroclash")  // persistent volume oroclash-sqlserver-data
postgres.AddDatabase("identitydb") // pgAdmin + persistent
redis persistent + rabbitmq management
identity-api container localhost/oroidentityserver:latest http:5080 https:5086
  + WithHttpsCertificateConfiguration (Aspire dev cert) + BindMount .oidc-certs → x509stores + Volume identity-dp-keys
oroclash-api project reference oroclashDb+rabbitmq+redis, env Identity__Authority=http://identity-api:5080
quizarena-admin project reference api+redis http:5008 https:7172 env Oidc__Authority + Identity__ClientSecret
oroclash-seeder project reference oroclashDb WaitFor api+identity
quizarena-player AddJavaScriptApp (dev, pnpm + ng serve :4200) / AddDockerfile (publish, nginx :80)
```

- **Dev:** `aspire start` (dashboard `https://localhost:17113`) → `sqlserver`/`postgres`/`redis`/`rabbitmq(management)`/`identity-api`/`oroclash-api`/`quizarena-player`/`quizarena-admin` + seeder one-shot.
- **Publish:** `aspire publish` genera `Docker Compose / Kubernetes / Azure Container Apps` manifests. `quizarena-player` usa `src/Player/QuizArena.Player/Dockerfile` multi-stage.
- **Secrets:** `symmetric-security-key` + `seed-admin-password` + `quizarena-admin-oidc-secret` (Aspire `AddParameter secret:true` → env `Identity__ClientSecret` / `Oidc__ClientSecret`).

---

## 7. Seguridad, calidad y observabilidad

| Aspecto | Implementación |
|---------|----------------|
| **AuthN** | OIDC discovery `/.well-known/openid-configuration` → `jwks_uri`, `authorization_code` + `refresh_token` + PKCE (Player público) / confidential + `refresh_token` (Admin). `sub` = `PlayerId`. |
| **AuthZ** | `RequireAuthorization("AdminOrRewardManager")` / `"Admin"` / anónima según slice + `AuthorizationBehavior` por `Permission` enum. |
| **Auditoría** | `AuditBehavior` + `AuditEntry` (Action/Permission/Resource/Result/CorrelationId). Consulta admin `GET /api/audit`. |
| **Idempotencia** | Header `X-Idempotency-Key` + `X-Correlation-Id` echo, storage `IdempotencyRecord` `UNIQUE (Key,ActorId)`. |
| **Concurrencia** | `RowVersion` + `409` ProblemDetails → UI `ConcurrencyError` banner con `Recargar`. |
| **Observabilidad** | `BuildingBlocks.ServiceDefaults` `AddServiceDefaults` → OTel (traces/metrics/logs), `GlobalExceptionHandler` RFC7807 `{type,title,status,detail,code,traceId,correlationId}`, health checks (`/health` usado por Aspire `WithHttpHealthCheck`). Dashboard OTel `aspire dashboard` / Jaeger futuro. |
| **Testing** | `dotnet test` 864+ passed (`Domain 272 + Application 131 + Arch 79 + Api 113 + Admin 269 + Infra 27`), `Vitest 3` Player 8 specs, `ng lint` eslint 9 + `@ngrx/eslint-plugin`, `validate-tokens.cjs` (0 hex). |
| **Design QA** | `design-system/GOVERNANCE.md`, `QUALITY-GATE.md`, validación tokens + `Architecture` `DesignSystemNoDirectDbTests`. |

---

## 8. Diagramas de infraestructura y flujo

### 8.1 Flujo de request (Player — canje)

```
Browser (Angular) ── PKCE authorize ─► OroIdentityServer (5080/5086) ── code ─► Browser ── tokens (sessionStorage)
Browser ── POST /api/rewards/{id}/redeem X-Idempotency-Key ─► oroclash-api:5000
  ├─ JWT validate jwks_uri (identity-api)
  ├─ AuthorizationBehavior (Player role)
  ├─ IdempotencyBehavior (cache lookup)
  ├─ Handler: Reward.ReserveStock → Game.ConsumePoints → RewardRedemption.Create → ledger UNIQUE → Outbox RewardRedeemed
  ├─ SaveChanges (RowVersion + Outbox) → RabbitMQ publish (eventual)
  └─ RFC7807 { AvailablePoints, Remaining, id/redeem } + X-Correlation-Id echo
Browser ← SignalR GameHub ScoreUpdated (si aplica)
```

### 8.2 Flujo Admin (crear recompensa — corregido 31-08)

```
RewardCreate.razor Validate() → RewardsServiceCore.CreateAsync(RewardForm) 
  → POST /bff/rewards { type,cost,stock,availableFrom,availableTo } 
  → YARP /api/rewards (Api)
  → CreateRewardEndpoint (fusiona Cost→PointsRequired, AvailableTo→ExpirationDate, Description?→string.Empty)
  → Handler Reward.Create((desc??"").Trim()) → EF Save → Response 200 { id, name, pointsRequired, stock, status, expirationDate }
  ← Cliente deserializa robusto: intenta V2, fallback a ApiRewardItem si legacy (evita excepción que mostraba "No se pudo crear")
  → Toast "Premio creado: X" → NavigateTo /admin/rewards
```

---

## 9. Estructura de repositorio

```
OroQuizClash.slnx
├── src/BuildingBlocks/{CQRS,EventBus,Kernel.Domain,Kernel.Infrastructure,Logger,ServiceDefaults,EventBus.RabbitMQ}
├── src/OroQuizClash.Domain/{Games,Categories,Questions,Rewards,Audit,Authorization}
├── src/OroQuizClash.Application/{Features/{Games,Rewards,Categories,Questions},Behaviors,Specifications}
├── src/OroQuizClash.Infrastructure/{Persistence,Services,Counters,Selection,Specifications}
├── src/OroQuizClash.Api/{Program.cs,Hubs/GameHub,Authorization}
├── src/Admin/{QuizArena.Admin, QuizArena.Admin.Client/{Pages,Components,Services,Models}}
├── src/Player/QuizArena.Player/{src/app/{core,features,stores,shared}, Dockerfile}
├── src/Seeder/OroQuizClash.Seeder/{Program.cs,SeedData.cs,Worker.cs}
├── src/IdentityServer/IdentityServer/Dockerfile → oroidentityserver:latest
├── design-system/{MASTER.md,tokens/design-tokens.{json,css},pages/*.md,overrides/*.md,GOVERNANCE.md}
├── specs/001-036/{spec.md,plan.md,research.md,data-model.md,contracts/,tasks.md}
├── OroQuizClash.AppHost/{AppHost.cs,appsettings.*}
├── scripts/register-admin-oidc-client.sh
└── tests/{Domain,Application,Architecture,Api,Infrastructure,Admin} (6 suites)
```

---

## 10. Referencias por archivo

- Constitución: `.specify/memory/constitution.md` (231 l)
- ADR: `docs/adr/ADR-010..013`
- AppHost: `OroQuizClash.AppHost/AppHost.cs:1-216`
- API Program: `src/OroQuizClash.Api/Program.cs`
- Dominio clave: `Domain/Games/Game.cs:15`, `Rewards/Reward.cs:8`, `Rewards/RewardRedemption.cs:10`
- Aplicación clave: `Features/Rewards/{CreateReward,RedeemReward,GetRewards}.cs`
- Infra clave: `Persistence/OroQuizClashDbContext.cs:17`, `Configurations/*TypeConfiguration.cs`
- Player: `Player/QuizArena.Player/src/app/{app.config.ts,app.component.ts,core/auth/auth.service.ts,features/rewards/*}`
- Admin: `Admin/QuizArena.Admin.Client/{Services/RewardsServiceCore.cs:136,Pages/Rewards/RewardCreate.razor:20}`

*Arquitectura vigente a 31-08-2026 (36 specs, net10.0, Angular 22, Aspire 9). Los fixes del 31-08 (Reward null-safe + V2 compat, Player logout fallback) forman parte de este documento.*
