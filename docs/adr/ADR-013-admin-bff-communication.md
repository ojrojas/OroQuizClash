# ADR-013: QuizArena Administration BFF Communication

**Status**: Accepted — 2026-08-28
**Drivers**: SPEC-017 (Admin App, constitution Principios VI/H), SPEC-016 (Design System)

## Context
The Admin app must provide privileged operations (games, categories, questions, players, rewards, reports, audit) without ever reaching QuizArena.Api directly from the browser. Tokens must never be readable by JavaScript (SC-003, FR-030), the browser must see a single origin, and live game monitoring must feel real-time while remaining correct (Server Truth). The solution must compose with Aspire's service discovery and health checks.

## Decision
1. **Single-origin YARP BFF catch-all** — `MapForwarder("/bff/{**catch-all}", "http://oroclash-api", AddPathRemovePrefix("/bff")+AddPathPrefix("/api") + Bearer transform).RequireAuthorization()` plus a second `MapForwarder("/hubs/game", ...)` for SignalR. The browser calls only `/bff/*` and `/hubs/game` on its own origin; the access_token is resolved per-request via `HttpContext.GetTokenAsync("access_token")` and attached server-side as `Authorization: Bearer`. No API URL appears in the WASM client (AdminBffTests enforces it).

2. **Shared, dual-implementation interfaces** — 10 interfaces in `QuizArena.Admin.Client/Services` define the contract (data-model §1, contracts/service-interfaces.md). `Client*Service` (WASM) calls `/bff/*` on the same origin with the cookie traveling automatically and a 401→login interceptor; `Server*Service` (InteractiveServer) calls `http://oroclash-api` (Aspire-resolved) with `BearerTokenHandler` (`IHttpContextAccessor` + `GetTokenAsync`). Both are registered behind the same interface so Razor components are mode-agnostic.

3. **Forwarded hub with service discovery** — The hub negotiate + WebSockets are proxied by YARP; WASM `HubConnectionBuilder().WithUrl("/hubs/game").WithAutomaticReconnect()` carries the session cookie, server wiring attaches the JWT extracted at subscribe time. `LiveGameSubscription` (aggregate-only: GameStarted/PlayerJoined/RoundStarted/RoundCompleted/GameFinished/LeaderboardUpdated) ignores the three private player events and raises `ResyncRequested` after reconnect so the UI re-queries REST before rendering (Server Truth, contracts/realtime.md §3).

## Alternatives rejected
| Alternative | Why rejected |
|---|---|
| Direct browser→Api with access_token in JS/SPA | Violates SC-003/FR-030; token exfiltration, no single origin, no HttpOnly cookie |
| Identity proxy / backend SDK instead of YARP | Added dependency, loses Aspire `AddHttpForwarderWithServiceDiscovery` + health check integration |
| Separate service per domain instead of catch-all | More forwarders to maintain; contract already normalizes all REST under `/api/{domain}` so one rewrite suffices |
| SignalR direct to Api from browser (no forwarder) | Would require exposing Api origin + token to JS; breaks single-origin guarantee |
| DbContext / Dapper in the Admin process | Violates constitution Addendum 2 & SPEC-016 FR-023 (guarded by DesignSystemNoDirectDbTests) |

## Consequences
- Tokens remain server-side (cookie OIDC + refresh in `ConfigureCookieOidc`); the WASM client receives only `AuthenticationState` claims (`AddAuthenticationStateSerialization`).
- All data flows through `ProblemDetails`→`ApiErrorException` (`ApiResponseExtensions`), so field errors render inline and 401 is handled uniformly (WASM → `/authentication/login`, Server → OIDC challenge).
- The `quizarena-admin` confidential client (`authorization_code` + `refresh_token` + PKCE) is the sole OIDC client; `must_change_password` (`AdminUserState`) blocks navigation until the provider's change-password form completes; the provider scope `admin` is requested via `Identity:ApiScope`.
- AppHost is the single source of truth: `.WithReference(api).WaitFor(api).WithEnvironment("Identity__Authority", identityServer.GetEndpoint("http")) + WithHttpHealthCheck("/health")`.
