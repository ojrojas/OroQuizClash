# Research: Player Application (027)

**Branch**: `027-player-application` | **Date**: 2026-08-28 | **Spec**: [spec.md](spec.md) | **Plan**: [plan.md](plan.md)

## R1 — Autenticación OIDC para Angular 22 SPA vs BFF

**Decision**: **PKCE público SPA como base para v1** (`angular-auth-oidc-client` 17+ o `@angular-architects/oauth2-oidc` con `authorization_code` + PKCE + `refresh_token` contra `identity-api/.well-known/openid-configuration`), con **BFF host `QuizArena.Player.Host` (YARP) como alternativa documentada** si la revisión de seguridad exige ocultar tokens del navegador (Constitución H). El plan en `plan.md` contempla ambas vías y deja la elección final al equipo en Plan→Tasks sin bloquear el diseño del resto de la feature.

**Rationale**:
- SPEC-027 y constitución VI mandan OIDC `authorization_code` + `refresh_token` contra OroIdentityServer discovery (`/.well-known/openid-configuration`, `jwks_uri`, `/connect/*`, `/auth/*`). Para un SPA público no hay `clientSecret` → el flujo estándar es `authorization_code` + **PKCE** (RFC 7636) + `refresh_token` con `offline_access` y rotación, exactamente lo que proveen `angular-auth-oidc-client` y `oauth2-oidc`.
- SPEC-017 (Admin) eligió BFF con cookie httpOnly + YARP forwarder para que el navegador nunca vea el `access_token` (Constitución H). Para Player, el mismo argumento aplica si el riesgo de robo de token en memoria se considera alto. Sin embargo, el mandato explícito es **Angular 22** puro y el placeholder `src/Player/QuizArena.Player` no tiene host .NET; imponer un BFF añade un proyecto `net10.0` y complejidad Aspire que puede evitarse en v1 si el `access_token` vive solo en memoria (no `localStorage`) y el `refresh_token` usa `httpOnly` + `SameSite` + rotación del servidor.
- Ambas librerías soportan: discovery auto, `jwks_uri` validation, `refresh_token` silencioso (`tryRefresh`), `must_change_password` claim gating (redirección a `/auth/change-password`), `post_logout_redirect_uri` → `/connect/logout`, y `CorrelationId` propagation vía interceptor.

**Alternatives considered**:
- **BFF-only (YARP como Admin)**: tokens nunca en navegador, máxima conformidad H. Rechazado como único camino para v1 porque duplica infraestructura (host net10.0, YARP, cookie refresher) y oscurece el mandato Angular puro; se mantiene como alternativa si auditoría de seguridad lo exige.
- **Implicit flow**: descartado (deprecated, sin `refresh_token`, inseguro).
- **Backend-for-Frontend con cookies pero sin PKCE**: solo viable si hay host .NET; para SPA pura no aplica.

**Implications for design**:
- `app.config.ts` → `provideAuth({ config: { authority: identityAuthority, clientId: 'quizarena-player', responseType: 'code', scope: 'openid profile email offline_access api', useRefreshToken: true, ... } })` o `OAuthService.configure(...)`.
- Guards: `AuthGuard` (isAuthenticated) + `MustChangePasswordGuard` (claim `must_change_password`).
- Interceptor: adjunta `Authorization: Bearer <access_token>` solo a `oroclash-api` (no a `identity-api` discovery), propaga `X-Correlation-Id`.
- Si se elige BFF, `QuizArena.Player.Host` replica `QuizArena.Admin/Program.cs` (OIDC Cookie + YARP `/bff/{**} → /api/{**}` + SPA fallback `MapFallbackToFile`).

## R2 — Inventario de endpoints `oroclash-api` para Player

**Decision**: **Reusar slices existentes; no se requieren nuevos endpoints para v1** salvo `GET /api/games/{gameId}/players/me` (estado privado del jugador autenticado) si no existe ya. Verificación contra `OroQuizClash.Api` + `OroQuizClash.Application/Features`.

**Rationale**:
- Spec y constitución J listan: `POST /api/games`, `POST /api/games/{id}/players` (`JoinGame`), `POST /api/games/{id}/start`, `GET /api/games/{id}`, `GET .../rounds/current`, `GET .../questions/current`, `POST .../answers`, `POST .../withdraw`, `GET .../leaderboard`, `GET /api/rewards` etc. Ya implementados o en SPEC-004/005/006/011.
- Para Player se consume: `JoinGame` (lobby), `GetGame` + `GetCurrentRound` + `GetCurrentQuestion` (hidratación), `SubmitAnswer` (idempotente, FR-009), `WithdrawPlayer` (retiro), `GetPlayerScore`/`GetSecuredScore` (ledger), `GetLeaderboard` (opcional, SPEC-011), y polling/rehidratación `GET /api/games/{id}/players/me` (contexto de 10 elementos).
- Si `players/me` no existe, se añade como slice `GetMyPlayerState` (Query + Handler + Endpoint `IEndpoint`) siguiendo Vertical Slice (FR-017/FR-019). No rompe principios.

**Alternatives**: Crear nuevo agregado Player — rechazado (ya existe `GamePlayer` domain).

## R3 — NgRx SignalStore para contexto privado por instancia

**Decision**: **Un SignalStore raíz `PlayerGameStore` por `GameSession`** con `withState` para los 10 elementos + `withComputed` para derivados (e.g. `remainingSeconds`, `isExpired`, `isTerminal`) + `withMethods` + `withProps` + `rxMethod` para efectos (hidratación, submitAnswer, rehydrate, timer tick, SignalR handlers) y `patchState`/`tapResponse` para actualizaciones granulares. Vida del store = vida de la sesión (scoped por `GameSessionId`).

**Rationale**:
- Skill `ngrx-signal-store` instalada: `signalStore(withState, withComputed, withMethods, withProps)` + `DeepSignal` tracking + `rxMethod` + `withEntities` si se requiere colección de rounds.
- FR-003 exige store dedicado por sesión sin compartir estado mutable entre jugadores/juegos → `PlayerGameStore` se provee en `GameComponent` con `providers: [PlayerGameStore]` (scoped), no `providedIn: 'root'` global.
- `Timer` derivado: `remainingSeconds = computed(() => Math.max(0, Math.floor((expiresAt().getTime() - now())/1000)))` con `now` signal actualizado por `interval(1000)` + corrección periódica contra server `expiresAt`.
- Efectos: `hydrate = rxMethod<void>(pipe(switchMap(() => api.getMyState()), tapResponse({ next: s => patchState(store, s), error: e => patchState(store, { error: toProblemDetails(e) })})))`.
- Instalación: `npm install @ngrx/signals @ngrx/signals/entities @ngrx/signals/rxjs-interop` (nota 4). Ver `contracts/signal-stores.md` para slices.

**Alternatives**:
- `BehaviorSubject`/services manuales — rechazado (sin granularidad DeepSignal, más boilerplate, sin `rxMethod`).
- NgRx Store clásico (actions/reducers/effects) — rechazado (verboso, no aprovecha signals Angular 22).
- `withEntities` global — solo para colecciones (leaderboard, historial de rondas), no para contexto único por jugador.

## R4 — Tiempo real con SignalR desde Angular

**Decision**: `@microsoft/signalr` `HubConnectionBuilder` con `withUrl('/hubs/game', { accessTokenFactory: () => oauthService.getAccessToken() })` + `withAutomaticReconnect([0,2000,5000,10000])` + `keepAliveInterval` 15s. Eventos `RoundStarted`, `QuestionAvailable`, `ScoreUpdated`, `RoundCompleted`, `GameFinished` en `realtime-contracts.md`. Política: evento → rehidratación REST (fuente de verdad), no aplicar estado directo del evento (Constitución V).

**Rationale**: Reusar `GameHub` existente (SPEC-012) ya usado por Admin live games. Angular `GameRealtimeService` expone `Observable<GameEvent>` y delega a `PlayerGameStore.rehydrate()`/`hydrateRound()` en cada evento relevante.

## R5 — Timer autoritativo y corrección de drift

**Decision**: Cliente muestra countdown derivado de `expiresAt` (server timestamp ISO 8601 UTC) + `serverNow` ocasional. Sincronización: en cada `hydrate`/`QuestionAvailable` se guarda `serverExpiresAt`; `now` local avanza con `interval(1000)` y se corrige cada 10s o al recibir evento (`QuestionAvailable` incluye `expiresAt`). Decisión de expiración solo server (FR-012).

## R6 — Diseño responsive y accesibilidad (SPEC-016)

**Decision**: Consumir `design-system/tokens/design-tokens.css` vía import en `angular.json` styles + `data-theme="player"` (override `design-system/overrides/player.md` — ya documentado como pendiente en `src/Player/QuizArena.Player/README.md`). Tokens para spacing/typography/color sin literales; componentes con `aria-live="polite"` para `Timer`/`Score`/`Status`, foco visible, teclado, targets ≥44px, skeleton/empty/error/expired/terminal states (FR-020/021).

## R7 — Estructura Angular 22 y orquestación Aspire

**Decision**: `src/Player/QuizArena.Player` creado con `ng new --standalone --style=css --routing` (Angular 22). `AppHost` añade `builder.AddNpmApp("quizarena-player", "../src/Player/QuizArena.Player", args: ["start"])` en dev o `AddProject<Projects.QuizArena_Player_Host>` en prod si BFF. Puerto dev 4200 proxy a `oroclash-api` vía `proxy.conf.json` (`/api → http://localhost:5000`).

## R8 — Testing de SignalStores

**Decision**: `vitest` + `TestBed.configureTestingModule({ providers: [PlayerGameStore] })` + `inject(PlayerGameStore)` en specs. Tests: estado inicial 10 elementos, transiciones `patchState`, efectos `rxMethod` con `HttpTestingController`, computed `remainingSeconds`/`isTerminal`, aislamiento entre instancias (dos `TestBed` con stores distintos).

## Resolved NEEDS CLARIFICATION

- Auth flow: resuelto R1 (PKCE SPA base, BFF alternativo documentado).
- Endpoints: resuelto R2 (reuso, `players/me` condicional).
- Store shape: resuelto R3 (store por GameSession, derived signals para Timer/Status).
- Realtime: resuelto R4 (SignalR con rehidratación REST).
- Timer: resuelto R5 (server expiresAt + interval + periodic correction).
