# OroQuizClash — QuizArena

Multiplayer trivia game platform — **Modular Monolith** `net10.0` (BuildingBlocks) + **OroIdentityServer** (Podman OIDC) + **QuizArena.Admin** (`Blazor .NET 10` BFF) + **QuizArena.Player** (`Angular 22` SignalStore). Constitución v1.1.0 (I-VI, A-J) + SDD `Constitution → Spec → Plan → Tasks → Implementation`.

**Estado actual**: `036-player-rewards` `Ready for Review` — 36 specs implementadas (001-036). `dotnet build` 0 errors, `dotnet test` 864+ passed 0 failed, `QuizArena.Player` 028-036 completos (Lobby → Game → Rounds → Withdrawal → Rewards).

## Arquitectura

| Capa | Stack | Contrato |
|------|-------|----------|
| **Backend** | `OroQuizClash.Api` + `Application` + `Domain` + `Infrastructure` — `net10.0`, `C#12`, `Clean Architecture` `Web→Application→Domain←Infrastructure`, `DDD` (`AggregateRoot`, `IBusinessRule`, `Enumeration`), `Vertical Slice` + `CQRS` (`ICommand`/`IQuery`/`ISender` BuildingBlocks), `EfRepository` + `Specification` + `AppDbContextBase` + Outbox | `BuildingBlocks.Kernel.Domain`/`CQRS`/`EventBus.RabbitMQ`/`ServiceDefaults` (OTel, health, `IEndpoint`, `GlobalExceptionHandler` RFC7807). No `MediatR`/`MassTransit`/`AutoMapper` |
| **DB** | SQL Server primaria `oroclash` (`Game`, `GamePlayer`, `GameRound`, `Reward`, `RewardRedemption`, `PointTransaction`, `Outbox`); PostgreSQL `identitydb` aislada (OroIdentityServer) | `IRepository<T,TId>`, `IUnitOfWork`, `rowversion` optimista, `UNIQUE (GameId,UserId)`, `UNIQUE (PlayerId,IdempotencyKey)` |
| **Realtime** | `SignalR` `GameHub` (`RoundStarted/QuestionAvailable/ScoreUpdated/RoundCompleted/GameFinished/PlayerWithdrawn`) `withAutomaticReconnect [0,2000,5000,10000,30000]` → `hydrate` REST (Server Truth V) | `RabbitMQ` solo integración (`RewardRedeemed`, `GameFinished` via Outbox) nunca source of truth |
| **Identidad** | `OroIdentityServer` Podman `oroidentityserver:latest` (`OpenIddict 8`, `postgres:identitydb`, `Blazor FluentUI`) — única autoridad usuarios/auth | `/.well-known/openid-configuration` → `jwks_uri`, `authorization_code` + `refresh_token` + PKCE (Player) / `authorization_code` confidencial + `refresh_token` (Admin), `sub`=`PlayerId`, `must_change_password` gating |
| **Orquestación** | `OroQuizClash.AppHost` `Aspire 9` | `sqlserver`/`postgres`/`redis`/`rabbitmq`/`identity-api:5080`/`oroclash-api:5000`/`quizarena-player:4200`/`quizarena-admin:7172` |
| **Specs** | 036 specs `specs/001-036/` | SDD + `specs/<nnn>-<name>/` `spec.md`/`plan.md`/`research.md`/`data-model.md`/`contracts/`/`tasks.md` |

**Solución**: `OroQuizClash.slnx` → `/src/` (`Api`,`Application`,`Domain`,`Infrastructure`) + `/src/Admin/` (`QuizArena.Admin.Client`/`QuizArena.Admin`) + `/src/BuildingBlocks/` (7 libs) + `/tests/` (6 suites) + `OroQuizClash.AppHost`.

## UI/UX Design System (SPEC-016, ADR-012)

- **Source of truth**: `design-system/MASTER.md` (shared) + `design-system/overrides/{admin,player}.md` + 11 `design-system/pages/*.md` overrides
- **Tokens**: three-layer `primitive→semantic→component` `design-system/tokens/design-tokens.{json,css}`; themes `[data-theme="administration"|"player"]`; 0 hex literals fuera de tokens
- **Palette**: quiz blue `#2563EB` + gold `#F59E0B`; Admin light enterprise (`#1E40AF`/Fira), Player dark cinematic (`#0F172A`/Russo One + Chakra Petch)
- **Responsive**: 375/768/1024/1440/1536 normative (adaptive); WCAG 2.2 AA ambos themes; `reduce-motion` + `forced-colors`; targets ≥44px, `role="dialog"` `aria-live`, `angular.json` styles `design-system/tokens/design-tokens.css`
- **Governance**: `design-system/GOVERNANCE.md`, quality gate `QUALITY-GATE.md`, `node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir design-system/` (0 literals) + `dotnet test Architecture` (`DesignSystemNoDirectDbTests`)
- **Spec**: `specs/016-ui-ux-design-system/` | **ADR**: `docs/adr/ADR-012`

## Backend — Modular Monolith (net10.0)

**Domain** (`OroQuizClash.Domain`): Agregados `Game` (`WithdrawPlayer`, `ConsumePoints`, `StartRound`, `SubmitAnswer`, `Finish`), `GamePlayer` (`Score.CurrentPoints`), `GameRound`, `Reward` (`ReserveStock`/`ReleaseStock`), `RewardRedemption` (`Create` `REQUESTED` + `CreateAsConsolation` `APPROVED`), `PointTransaction` (`REWARD_REDEMPTION`/`WITHDRAWAL`/`CONSOLATION`), `Category`/`Question`. Políticas configurables `WithdrawalPolicy KEEP_SECURED_SCORE`, `LossPolicy`, `ConsolationPolicy`, `RewardStatus ACTIVE/INACTIVE` `RedemptionStatus REQUESTED→APPROVED→DELIVERED`.

**Application** (`OroQuizClash.Application`): Vertical Slices `Features/{Games,Rewards,Categories,Questions}/` `Command+Validator+Handler+Response+IEndpoint` thin `ISender`. Ejemplos: `RedeemReward` (`Reward.ReserveStock` → `Game.ConsumePoints` → `RewardRedemption.Create` + `REWARD_REDEMPTION` ledger `UNIQUE (PlayerId,IdempotencyKey)` → `RewardRedeemed` Outbox), `GetRewards` (`AvailablePoints` per `sub` desde `GamePlayer.Score`), `GetPlayerRedemptions`, `WithdrawPlayer` (`KEEP_SECURED_SCORE` `Current=Secured`), `SubmitAnswer`, `GetMyPlayerState` (3 métricas `Current/Secured/Potential`).

**Infra** (`OroQuizClash.Infrastructure`): `AppDbContextBase` + `EfRepository` + `SpecificationEvaluator` + Outbox + `*TypeConfiguration` (`Reward`, `RewardRedemption` `RowVersion`, `UNIQUE (PlayerId,IdempotencyKey)`), `Game` `RowVersion` per `GamePlayerId`.

**API** (`OroQuizClash.Api`): REST `GET /api/rewards?gameId` (Available/Required/Status), `POST /api/rewards/{id}/redeem` `X-Idempotency-Key` `X-Correlation-Id` `Bearer`, `GET /api/redemptions`, `POST /api/games/{id}/withdraw`, `GET /api/games`, `POST /api/games/{id}/players`, `GET /api/games/{id}/players/me`, `POST /api/games/{id}/answers`, `/hubs/game`. JWT `jwks_uri` OroIdentityServer, `RequireAuthorization`, RFC7807 `ProblemDetails` `{type,title,status,detail,code,traceId,correlationId}` + `X-Correlation-Id` echo, `GlobalExceptionHandler`.

## QuizArena.Admin — Administration (SPEC-017, ADR-013)

`net10.0` **Blazor Web App** (Auto interactivity, **BFF via YARP**). No acceso directo a DB — vía `QuizArena.Api`.

- **Arquitectura**: YARP catch-all `/bff/{**}→/api/{**}` + `/hubs/game` forward. Contratos compartidos `QuizArena.Admin.Client/Services`, dual `Client*Service → /bff` (cookie + 401→login) vs `Server*Service → http://oroclash-api` (Aspire discovery + `BearerTokenHandler` `GetTokenAsync("access_token")`). Tokens server-side (HttpOnly OIDC cookie + refresh); WASM solo claims.
- **OIDC**: Confidencial `quizarena-admin` (`authorization_code` + `refresh_token` + PKCE) contra `OroIdentityServer`. Scopes `openid profile offline_access roles admin`; `must_change_password` → `Account/ChangePassword`. Registro: `./scripts/register-admin-oidc-client.sh` (`specs/017-admin-application/contracts/oidc-config.md`); `aspire start` inyecta `Identity__Authority`/`ClientSecret`/`ApiScope=admin` vía `quizarena-admin-oidc-secret`.
- **Run (Aspire)**: `aspire start` (postgres+pgAdmin, sqlserver, redis, rabbitmq, `identity-api 5080/5086`, `oroclash-api 5000`, `quizarena-admin https://localhost:7172`). Health `/health`, `/alive`.
- **AppHost**: `builder.AddProject<Projects.QuizArena_Admin>("quizarena-admin").WithReference(api).WaitFor(api).WithEnvironment("Identity__Authority", identity.GetEndpoint("http"))`
- **ADRs**: `ADR-013-admin-bff-communication.md` | Specs `specs/017-026/` (Admin Dashboard, Game Config, Categories, Questions, Game Ops, Rewards, Players, Reporting, Audit)

## QuizArena.Player — Player SPA (Angular 22) — SPEC-027..036

`src/Player/QuizArena.Player/` — **Angular 22 standalone** (`input()`/`signal()`/`computed()` `@if/@for`, `provideRouter`, `HttpClient withFetch` `withInterceptors`), **NgRx Signals 22** (`signalStore` `rxMethod` `tapResponse` `sessionStorage` idempotencia), **angular-auth-oidc-client 17** PKCE, **SignalR 8**, `design-system/tokens` `data-theme="player"`.

**Rutas** (`src/app/app.routes.ts`):

| Ruta | Componente | Guard | Descripción |
|------|------------|-------|-------------|
| `/`, `/lobby`, `/player/lobby` | `LobbyComponent` | `authGuard`+`mustChangePasswordGuard` | Available Games 8 cols + join (028) |
| `/lobby/:gameId`, `/player/lobby/:gameId` | `GameDetailComponent` | auth | Detalle 8 cols + extended |
| `/game/:gameId` | `GameComponent` | auth | Cinematic 3 áreas + withdrawal (029) |
| `/result/:gameId` | `ResultComponent` | auth | Resultado final (034) |
| `/rewards` | `RewardsCatalogComponent` | auth | Wallet + catálogo 4 métricas (036) |
| `/rewards/history` | `RedemptionHistoryComponent` | auth | Historial `RequestedAt` desc (036) |
| `/rewards/:rewardId` | `RewardDetailComponent` | auth | Detalle + canje 2 pasos (036) |
| `/auth/callback`, `/auth/logout-callback` | `CallbackComponent` | — | OIDC PKCE |

**Stores**: `player-game.store.ts` (10 elementos `hydrate` `submitAnswer` `withdraw` `isTerminal` `canAnswer`), `player-rounds.store.ts` (`buildLadder 1..N`), `player-rewards.store.ts` (036: `wallet {availablePoints}` `catalog` `history` `redeem()` `idemp-redeem-{rewardId}`), `answer-interaction.store.ts`.

**Features**:
- **028 Lobby**: `Available Games` 8 cols `GET /api/games?WAITING_FOR_PLAYERS` paginado, `Join` `idemp-join-{gameId}` `POST /games/{id}/players`
- **029 Game**: `Current Round "Ronda 3/10"` + `Timer` + `Question 4 Answers radiogroup` + `ScorePanel` `Potential Reward` + `Leaderboard`; cinematic `280px 1fr` responsive
- **030 Rounds**: Ladder vertical `completed/current/upcoming` `isSecured` `isFinal`
- **035 Withdrawal**: `WithdrawalComponent` diálogo `Current/Secured/Potential` 3 métricas `GET /players/me`, 2 warnings, 2 pasos `idemp-withdraw-{gameId}` `POST /withdraw` `WITHDRAWN` `isTerminal` `Current=Secured`
- **036 Rewards** ⭐: **Points Wallet** `Available` `GET /api/rewards?gameId` → **Rewards Catalog** grid `1→2→4 col` `Required`/`Reward Status` `Canjeable/Puntos insuficientes/Agotada/No disponible` `Remaining Quedan 400/Te faltan 700` → **Reward Detail** 4 métricas + **Redeem 2 pasos** `role="dialog"` `X-Idempotency-Key idemp-redeem-{rewardId}` → `POST /api/rewards/{id}/redeem` (`ReserveStock`+`ConsumePoints` ledger `UNIQUE`) → **Confirmation** `Canjeada` `Reference` + **Redemption History** `GET /api/redemptions` paginado + **Consolation** `app-consolation-badge` `CreateAsConsolation` `APPROVED` `points 0` `var(--color-info)`

**APIs**: `games.api.ts` + `rewards.api.ts` (`getRewards`, `getMyRedemptions`, `redeem` `X-Idempotency-Key`), interceptores `X-Correlation-Id` + `Bearer secureRoutes` + RFC7807 `429 Retry-After`.

Ver `src/Player/QuizArena.Player/README.md` para detalle completo por feature.

## Specs — 001 → 036

| Grupo | Specs | Estado |
|-------|-------|--------|
| **Core** | 001 Game Config, 002 Categories, 003 Question Bank, 004 Game Lifecycle, 005 Round Engine, 006 Answer Evaluation, 007 Scoring | Done |
| **Player Core** | 008 Player Withdrawal, 009 Reward Redemption, 010 Consolation, 011 Multiplayer, 012 Realtime, 013 Security, 014 Audit, 015 Reporting | Done |
| **Design** | 016 UI/UX Design System | Done — `MASTER.md` + tokens + `QUALITY-GATE` |
| **Admin** | 017 App, 018 Dashboard, 019 Game Config, 020 Categories, 021 Question Bank, 022 Game Ops, 023 Rewards, 024 Players, 025 Reporting, 026 Audit | Done — `Blazor` BFF |
| **Player** | 027 App, 028 Lobby, 029 Game, 030 Rounds, 031 Answering, 032 Scoring, 033 Multiplayer, 034 Results, **035 Withdrawal**, **036 Rewards** | **036 Ready for Review** — `Angular 22` + `SignalStore` + `data-theme="player"` |

Detalle: `specs/036-player-rewards/` `spec.md` (4 US P1/P2, 13 FR, 8 SC) `plan.md` `research.md` (5 decisiones) `data-model.md` `contracts/api-contracts.md` `ui-contracts.md` `quickstart.md` V1-V7 `tasks.md` 43 tasks `[x]`.

## Quickstart

```bash
# Requisitos: .NET 10 SDK, Node 22, Aspire workload, Podman, Angular CLI 22
dotnet workload install aspire
podman build -f src/IdentityServer/IdentityServer/Dockerfile -t oroidentityserver:latest .
export symmetric_security_key="$(openssl rand -base64 32)"
export seed_admin_password="Admin@123456"

# Orquestación (sqlserver, postgres, redis, rabbitmq, identity-api:5080/5086, oroclash-api:5000, quizarena-player:4200, quizarena-admin:7172)
aspire start
# Health: http://localhost:5000/health, https://localhost:7172/health, http://localhost:5080/.well-known/openid-configuration

# Admin OIDC (una vez)
./scripts/register-admin-oidc-client.sh

# Player SPA
cd src/Player/QuizArena.Player
npm install
cp src/environments/environment.example.ts src/environments/environment.ts
# apiUrl=http://localhost:5000/api, identityAuthority=http://localhost:5080, gameHubUrl=http://localhost:5000/hubs/game
npm start # http://localhost:4200 proxy /api → 5000

# Admin Blazor (si no via Aspire)
dotnet run --project src/Admin/QuizArena.Admin
```

**Validación 036** (quickstart.md V1-V7): `/rewards?gameId` Wallet 1200 vs Catalog 800 `Canjeable Quedan 400` / 1500 `Puntos insuficientes Te faltan 300` → Detail 4 métricas → Redeem 2 pasos `X-Idempotency-Key` → History `RequestedAt` desc → Consolation `var(--color-info)` badge.

## Tests & Calidad

```bash
dotnet build OroQuizClash.slnx          # 0 errors, net10.0
dotnet test                             # 864+ passed 0 failed (Domain 272 + Application 131 + Arch 79 + Api 113 + Admin 269 + Infra 27)
  --filter Rewards                      # RedeemReward/Redeem idempotencia, GetRewards AvailablePoints
cd src/Player/QuizArena.Player
npm test -- --watch=false               # Vitest 3 + Testing Library (jsdom) 8 specs rewards/* + withdrawal/rounds
ng lint                                 # eslint 9 + @ngrx/eslint-plugin withState/withComputed/withMethods
node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir design-system/  # 0 literals
```

Cobertura: `Domain Unit` (`Game.WithdrawPlayer` `Reward.ReserveStock` `Game.ConsumePoints`), `Application` (`RedeemReward` ledger `UNIQUE`), `Integration` (`Testcontainers.MsSql` `RowVersion`), `Architecture` (Domain ↛ Angular, BuildingBlocks no reinvención), `Api Contracts` (`GET /rewards` 4 métricas, `POST /redeem` 409 mapping).

## Constitución (v1.1.0)

`I Domain First` (reglas en `Domain`, ej. `Game.ConsumePoints` `Reward.ReserveStock`), `II Clean Architecture` (`Web→App→Domain←Infra`), `III BuildingBlocks` (no `MediatR`/`AutoMapper`), `IV Vertical Slice CQRS` (`Features/{Games,Rewards}/`), `V Server Truth` (`Available` solo vía ledger `REWARD_REDEMPTION` `WITHDRAWAL`, `sub` auth), `VI OroIdentityServer` (Podman `oroidentityserver:latest` única autoridad).
Ver `.specify/memory/constitution.md` (231 líneas) + ADRs `docs/adr/ADR-010..013`.

## Estructura Repo

```
OroQuizClash.slnx
├── src/BuildingBlocks/           # Kernel.Domain, CQRS, EventBus.RabbitMQ, Kernel.Infrastructure, ServiceDefaults
├── src/OroQuizClash.Domain/      # Game, Reward, RewardRedemption, PointTransaction, Category, Question
├── src/OroQuizClash.Application/ # Features/Games, Features/Rewards, Behaviors, Specifications
├── src/OroQuizClash.Infrastructure/ # AppDbContextBase, Configurations, Repositories, Outbox
├── src/OroQuizClash.Api/         # IEndpoint, GlobalExceptionHandler, Hubs/GameHub
├── src/Admin/QuizArena.Admin(.Client)/ # Blazor BFF + YARP + OIDC quizarena-admin
├── src/Player/QuizArena.Player/  # Angular 22 SPA (ver README Player)
├── src/IdentityServer/           # Dockerfile → oroidentityserver:latest
├── design-system/                # MASTER.md, tokens, pages, overrides
├── specs/001-036/                # SDD specs (spec.md→plan.md→tasks.md)
└── OroQuizClash.AppHost/         # Aspire orquestación
```

## Docs & ADRs

- `docs/adr/ADR-010-game-configuration.md` … `ADR-013-admin-bff-communication.md`
- `design-system/GOVERNANCE.md`, `QUALITY-GATE.md`
- `specs/016-ui-ux-design-system/` + `specs/027-036/` Player flow

## Roadmap

- `036-player-rewards` `Ready for Review` → PR review + `quickstart.md` V1-V7 manual → merge `main`
- Siguiente: Hardening `quickstart.md` + `aspire dashboard` OTel traces + `Player` E2E Playwright (si aplica)
