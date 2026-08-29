# Research: Player Lobby (028)

**Branch**: `028-player-lobby` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## Summary
0 `NEEDS CLARIFICATION` en spec; investigación consolida 5 decisiones para lobby de jugador: Available Games filtrado server-side paginado, Join Game idempotente con `X-Idempotency-Key` + `UNIQUE (GameId,UserId)`, View Information proyección `GET /api/games/{id}`, Leave Lobby navegación client-side sin side-effect, y responsividad WCAG 2.2 AA con `design-system/tokens`. Todo alineado a constitución v1.1.0 + BuildingBlocks net10.0 y patrones de SPEC-027 (Player SPA) y SPEC-004 (Game lifecycle).

## Decisions

### 1. Available Games como proyección paginada server-side `GET /api/games?status=WAITING_FOR_PLAYERS`

- **Decision**: Reusar slice existente `GetGamesQuery(Status, CategoryId, CreatedBy, Search, Page, PageSize)` + `GameFilterSpecification` (Where `Status==WAITING_FOR_PLAYERS`, `CategoryId` opcional, `Search` sobre `Name`, `Include` Players count) con paginación `page/pageSize` (default 20, max 50) y orden `CreatedAt desc` (StartTime). Cliente `GamesApi.getGames(status='WAITING_FOR_PLAYERS', page, pageSize)` via `HttpClient` + interceptors `X-Correlation-Id`/`Authorization: Bearer` (`secureRoutes=[apiUrl]`). Sin nuevo endpoint; contrato ya expone `PaginatedGamesResponse { items: GameSummaryDto[], totalCount, page, pageSize }`. `GameSummaryDto` mapea 8 campos: `gameId, name, categoryName, difficulty (InitialDifficulty), minRounds/maxRounds, players { current/max }, createdAt (StartTime ISO UTC), prize (Reward.name o "—"), status`.
- **Rationale**: FR-001/FR-002/FR-011 + SC-001/SC-002 exigen filtrado autoritativo y paginación sin traer todo a memoria (Constitución V, E con Specification + `ApplyAsNoTracking` + index `Status`). Reusar evita duplicar agregados y mantiene Vertical Slice.
- **Alternatives**: Nuevo endpoint `GET /api/lobby/games` (rechazado — duplica `GetGames`, viola BuildingBlocks No Reinvention); filtrado client-side (rechazado — viola V, expone juegos no disponibles; no escala a 100+).
- **Query pattern**: `HasIndex(Game.Status, CreatedAt)` ya en `GameTypeConfiguration`; `HasIndex(Configuration.CategoryId)` para filtro futuro.

### 2. Join Game idempotente con `X-Idempotency-Key` y `UNIQUE (GameId,UserId)`

- **Decision**: Reusar slice `JoinGameCommand(GameId, UserId)` + `JoinGameHandler` (`Game.JoinPlayer(userId, displayName)`) con `IRepository<Game,GameId>.FirstOrDefaultAsync(GameByIdWithPlayersSpecification)` + `IUnitOfWork.SaveChangesAsync`. Idempotencia: `X-Idempotency-Key` UUID generado client-side por `gameId` en `sessionStorage` (`idemp-join-{gameId}`) y enviado como header `X-Idempotency-Key` (mirrored body `idempotencyKey` opcional). Server verifica `GamePlayer` existente `UNIQUE (GameId,UserId)` → si ya existe, retorna 200 mismo `GameSession` (AlreadyJoined idempotente → 200, no 409 duplicado). `RowVersion` en `Game` protege concurrencia `GameFull` (segundo que excede `MaxPlayers` → `MaxPlayersRule.IsBroken()` → `409`). Validación `GameStatus==WAITING_FOR_PLAYERS` + `Players.Count < MaxPlayers` server-side (FR-010). `PlayerId` = `sub` del JWT (VI/H), no body.
- **Rationale**: FR-004/FR-005/FR-010 + SC-003/SC-004/SC-005 + constitución F (optimistic concurrency `rowversion`, idempotency `AnswerSubmissionId` pattern). `UNIQUE` + `RowVersion` ya existen en `GamePlayerTypeConfiguration`.
- **Alternatives**: `JoinGame` con `PlayerId` en body sin `sub` check (rechazado — viola H, permite suplantación, auditado como 403 `PlayerIdentityMismatch`); sin `X-Idempotency-Key` (rechazado — doble clic crea duplicado bajo race).
- **Error mapping**: `GameFull → 400` + `ProblemDetails code=GameFull`, `GameNotWaitingForPlayers → 400`, `AlreadyJoined → 200` (no 409 fantasma), `PlayerIdentityMismatch → 403`.

### 3. View Game Information como proyección `GET /api/games/{id}` de solo lectura

- **Decision**: Reusar `GetGameQuery(GameId)` + `GetGameHandler` (`FirstOrDefaultAsync(GameByIdSpecification)` con `Include(Rounds, Players)` `AsNoTracking`) que retorna `GameResponse` con 8 campos + extendidos (`TimeLimitPerQuestionSeconds, PointsPerRound, WithdrawalPolicy, LossPolicy, Players.Count`). Cliente `GamesApi.getGame(gameId)` en modal/página detalle; no crea nuevo slice. Mapeo explícito (no AutoMapper) en Handler. `Prize` resuelto: si `Configuration.RewardRules.Type` define `RewardId`, `IRepository<Reward,RewardId>.FirstOrDefaultAsync` → `Reward.Name` else "—". No expone `Answer`/`Score` de otros jugadores (FR-013).
- **Rationale**: FR-003/FR-013 + SC-007 + constitución J (DTOs, no entidades) + V (server truth al refrescar). Reusar mantiene consistencia con `GET /api/games` y SPEC-027 `games.api.ts`.
- **Alternatives**: Nuevo `GetLobbyGameDetailQuery` (rechazado — duplica `GetGame`, viola DRY); incluir `Prize` calculado en `Game` aggregate (rechazado — `Reward` es otro bounded context, proyección en Application es suficiente).

### 4. Leave Lobby como navegación client-side sin side-effect

- **Decision**: `Leave Lobby` es `Router.navigate(['/'])` o `location.back()` en `LobbyComponent` sin llamada `POST`/`DELETE` a API. No invoca `WithdrawPlayer` (explicit domain action, terminal, SPEC-008) ni muta `GamePlayer`. Accesible por teclado `Tab/Enter`, `aria-label="Salir del lobby"`, `min-height 44px`, funciona sin auth adicional. Si jugador ya tiene `GameSession ACTIVE` en un juego previo, permanece `ACTIVE` (no withdraw automático) — verificación por `GET /api/games/{prevId}/players/me` muestra status intacto.
- **Rationale**: FR-007/FR-008 + SC-006 + constitución I (no efecto colateral). Distinción explícita con `WithdrawPlayer` (FR-007 Out of Scope).
- **Alternatives**: `Leave Lobby` como `DELETE /api/games/{id}/players/me` (rechazado — crearía abandono implícito, viola C (Withdrawal policy configurable) y F (idempotencia), confundiría auditoría).

### 5. Responsive WCAG 2.2 AA y observabilidad con `design-system/tokens`

- **Decision**: Lobby consume `design-system/tokens/design-tokens.css` (ya en `angular.json` styles, `data-theme="player"` en `app.component.ts:5` per SPEC-027) sin literales. Tabla 8 columnas → en `≥1024px` `<table>` con `<th scope="col">`, en `<768px` tarjetas apiladas (`display:grid`, `gap:12px`) con mismos 8 campos, ambos `aria-live="polite"` para lista, `LoadingSkeleton` (isLoading), `EmptyState` (no disponibles), `ErrorState` (ProblemDetails `detail` + `CorrelationId/TraceId` + Retry). Targets ≥44px, foco visible `outline:2px solid var(--color-primary)`, teclado `Tab`→ filas `Enter`→Join/View. Interceptors: `correlationIdInterceptor` genera `X-Correlation-Id` UUID por request, `authInterceptor` adjunta Bearer solo a `apiUrl`, `errorInterceptor` mapea RFC 7807 y 401→`silentRenew` (PKCE). OTel `BuildingBlocks.ServiceDefaults` ya provee `/health`/`/alive`, logs con `CorrelationId/TraceId/GameId/PlayerId`.
- **Rationale**: FR-012/FR-014 + SC-008/SC-009 + constitución I/H/J + SPEC-016 `design-system/MASTER.md` `overrides/player.md` (WCAG, 375-1536, `specs/016-ui-ux-design-system`).
- **Alternatives**: Estilos inline por componente (rechazado — viola Design System, no pasa axe/Lighthouse); `X-Correlation-Id` solo en Join (rechazado — viola I, todo request debe auditarse).

## Resolved Clarifications

| Topic | Resolution |
|-------|------------|
| Available Games filter | `Status==WAITING_FOR_PLAYERS` server-side `GameFilterSpecification`; otros estados no aparecen. |
| Number of Rounds display | `MinRounds-MaxRounds` (ej. 5-10) si difieren, si no solo `MinRounds`. |
| Start Time source | `Game.CreatedAt` (ISO UTC) orden desc; cliente convierte a local relativo. |
| Prize placeholder | Si `Reward` no configurado → "—" sin romper layout. |
| Leave vs Withdraw | Leave es navegación sin API; Withdraw es `POST /withdraw` domain action separado. |
| Token expiry in lobby | 401 → `angular-auth-oidc-client` `silentRenew` + `useRefreshToken`; si falla redirect `connect/authorize`. |

## References

- `draft/constitution.md` §I-VI, §A-J, §H VI OroIdentityServer `jwks_uri`, §V Server Truth.
- `draft/game-concept.md` §A Game Lifecycle `WAITING_FOR_PLAYERS`, §C Configurable Rules.
- `draft/oroidentityserver-specification.md` OIDC PKCE discovery, `X-Correlation-Id`.
- `design-system/MASTER.md` + `overrides/player.md` + `tokens/design-tokens.css` WCAG 375-1536.
- `src/Player/QuizArena.Player` `app.routes.ts /lobby`, `games.api.ts` `getGames/joinGame/getGame`, `core/interceptors/`, `shared/ui/` (SPEC-027).
- `src/OroQuizClash.Application/Features/Games/` `GetGames` `GameFilterSpecification` `JoinGame` `GetGame` `GameClaims` `IEndpoint`.
- `OroQuizClash.AppHost/AppHost.cs` `quizarena-player` → `oroclash-api` `identity-api`.
- `specs/027-player-application/` `research.md` R1 PKCE R2 `players/me` R6 design-system.
