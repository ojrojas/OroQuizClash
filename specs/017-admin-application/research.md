# Research: QuizArena Administration Application

**Branch**: `017-admin-application` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. Fuente principal del patrón de comunicación: sample oficial [`dotnet/blazor-samples` 10.0 — BlazorWebAppOidcBffAutoYarpAspire](https://github.com/dotnet/blazor-samples/tree/main/10.0/BlazorWebAppOidcBffAutoYarpAspire) (mandato del usuario) + artículo MS "Secure an ASP.NET Core Blazor Web App with OpenID Connect (OIDC)" (pivots: with YARP and Aspire).

---

## R1. Patrón de comunicación BFF (Blazor Auto ↔ QuizArena.Api)

**Decision**: Patrón BFF del sample oficial, adaptado:
1. **Interfaces compartidas** (`I*Service`) + DTOs viven en `QuizArena.Admin.Client`; el proyecto server lo referencia (igual que el sample: `BlazorWebAppOidc` referencia `BlazorWebAppOidc.Client` y usa `IWeatherForecaster`).
2. **Implementación cliente** (`Client*Service`): `AddHttpClient<I*Service, Client*Service>(BaseAddress = HostEnvironment.BaseAddress)` → llama a rutas `/bff/*` del propio servidor (la cookie de sesión viaja automáticamente; el WASM nunca ve el token).
3. **Implementación server** (`Server*Service`): `AddHttpClient<I*Service, Server*Service>(BaseAddress = http://oroclash-api)` (Aspire service discovery) — para componentes en modo InteractiveServer; adjunta el Bearer por request vía `IHttpContextAccessor.GetTokenAsync("access_token")` (por eso el sample registra `AddHttpContextAccessor`).
4. **Forwarder BFF**: `AddHttpForwarderWithServiceDiscovery()` + `app.MapForwarder("/bff/{**catch-all}", "http://oroclash-api", transform)` con:
   - Path rewrite `/bff/{rest}` → `/api/{rest}`
   - `transformBuilder.AddRequestTransform`: `Authorization: Bearer {access_token de la cookie}`
   - `.RequireAuthorization()` (solo sesiones válidas usan el BFF)
5. **Registro dual**: el contenedor DI resuelve la misma interfaz con la implementación adecuada según el modo de render; los componentes inyectan `I*Service` sin conocer el transporte.

**Rationale**: Mandato explícito del usuario; el sample es la referencia oficial de Microsoft para Blazor Web App Auto + OIDC + BFF + YARP + Aspire. Mantiene el token fuera del navegador (Constitución H), un solo contrato de servicio para ambos modos de interactividad, y el API no requiere cambios (CORS innecesario: el navegador solo habla con su propio origen).

**Alternatives considered**:
- WASM → API directo con token en memoria/localStorage: rechazado — expone el JWT al navegador (XSS), requiere CORS, viola el patrón pedido.
- Minimal APIs server por endpoint (variante BFF sin YARP): rechazado — duplica ~70 rutas; YARP forwarder es la variante del sample para este caso.
- gRPC-Web: rechazado — el API es REST (Constitución J); sin beneficio que justifique el cambio.

---

## R2. Autenticación OIDC con OroIdentityServer (OpenIddict 8)

**Decision**:
- `AddAuthentication().AddOpenIdConnect(oidcOptions)` + `AddCookie(SignInScheme)`:
  - `Authority` = endpoint http de `identity-api` vía Aspire service discovery (config `Identity:Authority`, mismo patrón que `OroQuizClash.Api/Program.cs:82`)
  - `ClientId` = `quizarena-admin` (cliente confidencial registrado en OroIdentityServer), `ResponseType = code`, `SaveTokens = true`, `Scope` += `offline_access` + scopes del API
  - `MapInboundClaims = false`; `NameClaimType = "name"`, `RoleClaimType = "roles"` (el API y GameHub ya usan claims `roles`/`sub` nativos — `GameClaims`)
  - Callbacks por defecto `/signin-oidc`, `/signout-callback-oidc`, `/signout-oidc` (deben coincidir con el registro del cliente)
- **`CookieOidcRefresher`** (clase del sample, `ConfigureCookieOidc`): callback `OnValidatePrincipal` que renueva el access_token con el refresh_token sin interacción; si el refresh falla → sign-out. OpenIddict soporta `refresh_token` grant.
- **`must_change_password`**: claim del proveedor (Constitución J); `Routes.razor`/middleware lo detecta y redirige a `/Account/ChangePassword` del IdentityServer antes de permitir operaciones.
- **Serialización de claims al cliente**: `AddAuthenticationStateSerialization(options => options.SerializeAllClaims = true)` (server) + `AddAuthenticationStateDeserialization()` (cliente) + `PersistingAuthenticationStateProvider`/`PersistentAuthenticationStateProvider` del sample para fluir el AuthenticationState server→client.
- Login/logout: `MapGroup("/authentication").MapLoginAndLogout()` (endpoints del sample); la UI nunca implementa formularios de credenciales.

**Rationale**: Constitución VI/H/J — OroIdentityServer es la única autoridad; flujo authorization_code + refresh_token contra discovery; sin user store local. El sample demuestra exactamente este wiring con un proveedor OIDC genérico (no Entra-específico).

**Alternatives considered**:
- Login propio con username/password: prohibido (Constitución VI).
- Bearer en el cliente con `Microsoft.AspNetCore.Components.WebAssembly.Authentication`: incompatible con BFF; token en navegador.
- Client credentials para la app: rechazado para UI de usuario — perdería identidad del operador en auditoría del API.

**Pendiente de configuración (no bloquea diseño)**: registrar el cliente `quizarena-admin` en OroIdentityServer (confidential, code+refresh, redirect URIs, scopes). Se documenta en `contracts/oidc-config.md`; el seed puede automatizarse vía `/api/applications` del IdentityServer en el plan de tareas.

---

## R3. Realtime Live Games (SignalR a través del BFF)

**Decision**: Reenviar el hub existente con `MapForwarder`:
```
app.MapForwarder("/hubs/game", "http://oroclash-api", transform: Bearer access_token).RequireAuthorization()
```
- `HttpForwarder` soporta WebSockets → el handshake SignalR y el transporte WS se proxyan transparentemente.
- El cliente WASM/Server construye `HubConnection` con URL relativa `/hubs/game` (mismo origen; la cookie viaja en el handshake; el forwarder adjunta el JWT del usuario).
- `JoinGameGroup(gameId)` funciona sin cambios: el API ve el JWT real del operador y `GameClaims.IsOrganizer` (ADMIN/GAME_MANAGER) autoriza unirse a cualquier grupo `game-{gameId}`.
- Eventos consumidos por Live Games: `GameStarted`, `PlayerJoined`, `RoundStarted`, `RoundCompleted`, `GameFinished`, `LeaderboardUpdated` (agregados). `QuestionPresented`/`PlayerAnswered`/`ScoreUpdated` se ignoran en UI admin (privacidad SPEC-016 §11 — solo agregados).
- **Política Server Truth**: eventos son best-effort; tras reconexión o evento, re-consulta REST (`GET /api/games`, leaderboard) — igual que la política FR-015/019 del GameHub.

**Rationale**: Mantiene el patrón BFF (sin token en cliente), reutiliza el hub existente sin cambios de backend, y el modelo de grupos/organizer ya soporta administradores.

**Alternatives considered**:
- Conexión directa WASM → hub con `accessTokenFactory`: requeriría exponer el JWT al navegador (rompe BFF).
- Conexión server-side singleton + hub propio de re-broadcast: más componentes (hub nuevo, auth servicio-a-servicio, fan-out); el forwarder logra lo mismo con una línea. Se conserva como **fallback** si las pruebas de WebSockets vía forwarder fallaran.

---

## R4. Catálogo de interfaces de servicio

**Decision**: Una interfaz por sección funcional en `QuizArena.Admin.Client/Services/`, mapeada a endpoints existentes del API (inventariados en SPEC-001..015):

| Interfaz | Endpoints consumidos (vía /bff) |
|----------|--------------------------------|
| `IDashboardService` | `/api/reports/*` (agregados KPI) |
| `IGamesAdminService` | `/api/games` (GET list, POST create, GET {id}, POST start/cancel/finish/force-finish/open-lobby/ready, score adjust, leaderboard, players status) |
| `ICategoriesService` | `/api/categories` (GET/POST/PUT + activate/deactivate/publish/archive) |
| `IQuestionsService` | `/api/questions` (GET/POST/PUT + activate/deactivate/publish/archive) |
| `IPlayersService` | `/api/games/{id}/players/{pid}/status`, `/api/players/{pid}/consolation-history`, `/api/games/{id}/players/{pid}/state` |
| `IRewardsService` | `/api/rewards` (GET/POST/PUT + activate/deactivate) |
| `IRedemptionsService` | `/api/redemptions` (GET list/all + approve/reject/cancel/deliver) |
| `IReportsService` | `/api/reports/games|categories|questions|players|rewards|leaderboard` |
| `IAuditService` | `/api/audit` (GET list + GET {id}) |
| `ILiveGamesService` | `/api/games?status=...` + hub `/hubs/game` (suscripción eventos) |

Firmas detalladas en `contracts/service-interfaces.md`. Todas retornan DTOs del proyecto Client; errores como `Result`-like ligero o excepciones tipadas mapeadas de `ProblemDetails` (RFC 7807 passthrough del API).

**Rationale**: Espejo 1:1 de las capacidades del backend ya implementadas; una interfaz por sección alinea con navegación y user stories del spec.

**Alternatives considered**: Una mega-interfaz `IApiClient`: rechazada — acopla todas las secciones, dificulta DI/testing. Refit/source-gen: rechazado — añade dependencia sin beneficio para ~70 endpoints ya estables.

---

## R5. Integración Aspire (AppHost existente)

**Decision**: Extender `OroQuizClash.AppHost/AppHost.cs`:
```
var admin = builder.AddProject<Projects.QuizArena_Admin>("quizarena-admin")
    .WithReference(api).WaitFor(api)
    .WithEnvironment("Identity__Authority", identityServer.GetEndpoint("http"))
    .WithHttpHealthCheck("/health");
```
- Nombre de service discovery del API: `oroclash-api` (existente) → BaseAddress `http://oroclash-api` en forwarder y `Server*Service`.
- `Identity__Authority` = endpoint http de `identity-api` (5080) — misma convención que el API.
- El proyecto admin entra en `OroQuizClash.slnx`; `Projects.QuizArena_Admin` se genera por referencia de proyecto del AppHost.
- DataProtection: el servidor Blazor usa cookie auth → necesita keyring; en dev el dev-cert basta, en prod volumen (como identity).

**Rationale**: El AppHost es la única fuente de verdad del grafo (notas del AppHost); service discovery evita URLs hardcodeadas y habilita `aspire deploy`.

**Alternatives considered**: URLs por configuración sin Aspire: rechazado — el repo ya es Aspire-first; el sample usa exactamente service discovery (`https://weatherapi`).

---

## R6. Consumo del Design System (SPEC-016)

**Decision**:
- `App.razor` incluye `<link href="design-tokens.css">` (copiado a `wwwroot/` del admin como artefacto generado) y `<html data-theme="administration">`.
- Fuentes Fira Sans/Code vía Google Fonts (import definido en `design-system/tokens/typography.md`).
- Componentes UI implementados según `design-system/components/*.md` + pantallas `design-system/screens/admin-*.md` + overrides `design-system/pages/*.md` (todas las pantallas del spec 017 tienen pantalla SPEC-016 equivalente salvo Players y Rewards, que se modelan con los mismos componentes del catálogo).
- Gate CI: `validate-tokens.cjs --dir src/Admin` (0 literales hex fuera de tokens).
- Estados por pantalla: Loading/Ready/Empty/Error obligatorios (states.md); accesibilidad AA (a11y.md); responsive 375–1536 (responsive.md).

**Rationale**: SPEC-016 es fuente de verdad vinculante (Addendum 2 §3 Design System First); el tema administration ya define paleta/typografía/densidad.

**Alternatives considered**: Librería de componentes externa (MudBlazor/Radzen): rechazada — anti-patrón §13 "default library appearance"; el catálogo SPEC-016 cubre los 15 componentes necesarios.

---

## R7. Template net10.0 y flags del comando

**Decision**: Usar el comando exacto del usuario:
```
dotnet new blazor -f net10.0 -ai true -int Auto -o src/Admin/QuizArena.Admin
```
- `-f net10.0`: target único net10.0 (SDK 10.0.400 instalado, `rollForward latestFeature`).
- `-int Auto`: crea `QuizArena.Admin` + `QuizArena.Admin.Client` con interactividad Auto (Server durante la primera carga/prerender, WASM después con caché).
- `-ai true`: habilita la integración AI del template net10 (página/servicio de chat); se conserva por mandato del usuario aunque no es parte del alcance funcional admin (queda como scaffold, navegable pero no vinculada a flujos de negocio).
- Post-creación: ajustar csproj (ProjectReference a `BuildingBlocks.ServiceDefaults` y a `QuizArena.Admin.Client` ya creado por el template), añadir paquetes YARP + SignalR Client vía CPM (`Directory.Packages.props`).

**Rationale**: Mandato explícito del usuario; net10.0 es el target del backend y del SDK del repo.

**Alternatives considered**: net11.0 (mencionado en addendum como multi-target BuildingBlocks): descartado por instrucción explícita del usuario ("enfocalo solo en net10"). Server-only interactivity: descartado — el usuario pidió Auto.

---

## R8. Navegación por rol y denegación de secciones

**Decision**:
- `NavMenu` filtra secciones por claim `roles` (serializado al cliente vía `SerializeAllClaims = true`):
  - ADMIN → 10 secciones
  - GAME_MANAGER → Dashboard, Games, Game Configuration, Categories, Question Bank, Players, Live Games, Reports
  - REWARD_MANAGER → Dashboard, Rewards, Reports
- Rutas con `[Authorize]` + política local espejo de `SecurityPolicies` del API (p. ej., Audit requiere ADMIN); la UI oculta lo no permitido pero **la autoridad real es el API** (403 si un usuario accede directo).
- Denegaciones: página/estado "Acceso denegado" claro, sin fuga de datos (FR-008 spec 017).

**Rationale**: Constitución H (policy-based authorization desde claims) + mapeo de roles ya definido en `SecurityPolicies.PolicyRoles`.

**Alternatives considered**: Ocultar solo en UI sin políticas: rechazado — defensa en profundidad; el API ya protege, la UI además no ofrece el acceso.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | BFF con YARP forwarder catch-all `/bff/{**}` → `/api/{**}` + Bearer transform | Usuario + sample oficial |
| 2 | Interfaces compartidas en `.Client`, doble implementación Client/Server | Sample oficial |
| 3 | OIDC code+refresh vs OroIdentityServer, CookieOidcRefresher, claims nativos | Constitución VI/H/J + sample |
| 4 | SignalR Live Games vía MapForwarder del hub (WebSockets proxyados) | GameHub existente + BFF |
| 5 | Server Truth: re-consultar REST tras evento/reconexión | GameHub FR-015/019 |
| 6 | Aspire AppHost extiende grafo con `quizarena-admin` | AppHost existente |
| 7 | Design System SPEC-016 tema administration, gate validate-tokens | SPEC-016/Addendum 2 |
| 8 | net10.0 único, template blazor `-ai true -int Auto` | Usuario |
| 9 | NavMenu por rol + políticas espejo | SecurityPolicies |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
