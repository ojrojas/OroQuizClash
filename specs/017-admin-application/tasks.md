# Tasks: QuizArena Administration Application

**Input**: Design documents from `/specs/017-admin-application/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (bff-endpoints, service-interfaces, oidc-config, realtime), quickstart.md
**Branch**: `017-admin-application` | **Date**: 2026-08-28
**Organization**: Tasks grouped by user story (US1–US6); Setup + Foundational bloqueantes; parallelizable [P] flagged.
**Tests**: Incluidos (xUnit) — el plan los mandata (arquitectura no-DB, BFF wiring, validaciones de formulario); sin TDD exhaustivo por DTO.
**Plataforma**: net10.0 único (mandato usuario). Patrón BFF según sample `BlazorWebAppOidcBffAutoYarpAspire`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: puede ejecutarse en paralelo (archivos distintos, sin dependencias incompletas)
- **[Story]**: US1..US6 según spec.md
- Rutas exactas en cada tarea

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Crear la solución Blazor Auto net10.0 y su integración en el repo (slnx, CPM, AppHost, assets).

- [X] T001 Create los proyectos con el comando exacto del usuario: `dotnet new blazor -f net10.0 -ai true -int Auto -o src/Admin/QuizArena.Admin` (genera `src/Admin/QuizArena.Admin/` server + `src/Admin/QuizArena.Admin.Client/` WASM; conservar scaffold `-ai` sin vincular a flujos de negocio)
- [X] T002 Add ambos proyectos a la solución: `dotnet sln OroQuizClash.slnx add src/Admin/QuizArena.Admin src/Admin/QuizArena.Admin.Client`
- [X] T003 Add versiones CPM en `Directory.Packages.props`: `Yarp.ReverseProxy` 2.3.0, `Microsoft.Extensions.ServiceDiscovery.Yarp` 10.9.0 (provee `AddHttpForwarderWithServiceDiscovery`), `Microsoft.AspNetCore.Authentication.OpenIdConnect` 10.0.11, `Microsoft.AspNetCore.SignalR.Client` 10.0.11
- [X] T004 [P] Ajustar `src/Admin/QuizArena.Admin/QuizArena.Admin.csproj`: ProjectReference a `src/BuildingBlocks/BuildingBlocks.ServiceDefaults/BuildingBlocks.ServiceDefaults.csproj` y referencia a `QuizArena.Admin.Client`; PackageReferences `Microsoft.AspNetCore.Authentication.OpenIdConnect` y `Microsoft.Extensions.ServiceDiscovery.Yarp`. Ajustar `src/Admin/QuizArena.Admin.Client/QuizArena.Admin.Client.csproj`: PackageReference `Microsoft.AspNetCore.SignalR.Client`
- [X] T005 Integrar en `OroQuizClash.AppHost/AppHost.cs`: `builder.AddProject<Projects.QuizArena_Admin>("quizarena-admin").WithReference(api).WaitFor(api).WithEnvironment("Identity__Authority", identityServer.GetEndpoint("http")).WithHttpHealthCheck("/health")` (según research R5)
- [X] T006 [P] Copiar `design-system/tokens/design-tokens.css` a `src/Admin/QuizArena.Admin/wwwroot/design-tokens.css` (artefacto generado; fuente de verdad SPEC-016)
- [X] T007 Verificar compilación: `dotnet build OroQuizClash.slnx` debe pasar con los dos proyectos nuevos net10.0

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Contratos compartidos (modelos + interfaces), autenticación OIDC, infraestructura BFF (forwarders), shell de UI y componentes compartidos del Design System. **Bloquea todas las user stories** (sin esto ninguna sección puede existir).

**⚠️ CRITICAL**: Ninguna página de sección (US1–US6) puede implementarse antes de completar esta fase.

### Modelos y contratos compartidos (data-model.md + contracts/service-interfaces.md)

- [X] T008 [P] Create modelos comunes en `src/Admin/QuizArena.Admin.Client/Models/Common.cs`: `PagedResult<T>`, `ApiErrorView`, `ApiErrorException`, `DateRange`, `GameFilter`, `CategoryFilter`, `QuestionFilter`, `RedemptionFilter`, `AuditFilter` (records inmutables, JSON camelCase)
- [X] T009 [P] Create modelos de juegos en `src/Admin/QuizArena.Admin.Client/Models/GameModels.cs`: `GameSummary`, `GameDetail`, `GameConfigurationForm`, `RoundSummary`, `LeaderboardEntry`, `GameStatusView` (data-model §1 Games)
- [X] T010 [P] Create modelos de contenido en `src/Admin/QuizArena.Admin.Client/Models/ContentModels.cs`: `CategorySummary`, `CategoryForm`, `CategoryStatusView`, `QuestionSummary`, `QuestionForm`, `OptionForm`, `QuestionStatusView` (data-model §1 Categories/Questions)
- [X] T011 [P] Create modelos de jugadores/recompensas en `src/Admin/QuizArena.Admin.Client/Models/OperationsModels.cs`: `PlayerStatusView`, `ConsolationHistoryEntry`, `RewardSummary`, `RewardForm`, `RewardStatusView`, `RedemptionSummary`, `RedemptionStatusView` (data-model §1 Players/Rewards)
- [X] T012 [P] Create modelos operativos en `src/Admin/QuizArena.Admin.Client/Models/InsightModels.cs`: `ReportResult`, `AuditEntry`, `DashboardKpis`, `LiveGameSummary`, `LiveConnectionView`, `AdminUserState` (data-model §1/§2/§3)
- [X] T013 [P] Create las 10 interfaces compartidas en `src/Admin/QuizArena.Admin.Client/Services/` (firmas exactas en contracts/service-interfaces.md): `IDashboardService.cs`, `IGamesAdminService.cs`, `ICategoriesService.cs`, `IQuestionsService.cs`, `IPlayersService.cs`, `IRewardsService.cs`, `IRedemptionsService.cs`, `IReportsService.cs`, `IAuditService.cs`, `ILiveGamesService.cs` (+ `LiveGameSubscription`)

### Autenticación OIDC + BFF (contracts/oidc-config.md + bff-endpoints.md)

- [X] T014 Create `src/Admin/QuizArena.Admin/Services/CookieOidcRefresher.cs` con extensión `ConfigureCookieOidc(cookieScheme, oidcScheme)`: `OnValidatePrincipal` que renueva access_token con refresh_token y reemite cookie; sign-out si el refresh falla (patrón del sample oficial)
- [X] T015 Wire autenticación en `src/Admin/QuizArena.Admin/Program.cs`: `AddServiceDefaults()`, `AddAuthentication().AddOpenIdConnect(...)` (Authority `Identity:Authority`, ClientId `quizarena-admin`, `ResponseType=code`, `SaveTokens=true`, scopes `offline_access` + API scope, `MapInboundClaims=false`, NameClaimType `name`, RoleClaimType `roles`) + `AddCookie(SignInScheme)` + `ConfigureCookieOidc` + `AddAuthorization()` con políticas `AdminOnly`/`AdminOrGameManager`/`RewardManagerOrAdmin` (contrato oidc-config §2/§6)
- [X] T016 Create `src/Admin/QuizArena.Admin/Services/BffForwarderExtensions.cs`: `MapBffForwarder(this WebApplication app)` con `MapForwarder("/bff/{**catch-all}", "http://oroclash-api", transform)` — rewrite `/bff/{rest}`→`/api/{rest}` + `Authorization: Bearer {GetTokenAsync("access_token")}` + `.RequireAuthorization()`; y `MapGameHubForwarder()` con `MapForwarder("/hubs/game", "http://oroclash-api", Bearer transform).RequireAuthorization()` (contrato bff-endpoints §1/§2). Registrar en Program.cs `AddHttpForwarderWithServiceDiscovery()` + `AddHttpContextAccessor()`
- [X] T017 Create `src/Admin/QuizArena.Admin/Services/AuthenticationEndpoints.cs` con `MapLoginAndLogout()` (grupo `/authentication`: challenge login, sign-out local+OIDC) y mapear en Program.cs; completar pipeline (`UseAuthentication`/`UseAuthorization`/`UseAntiforgery`, `MapRazorComponents` + modos Server/WASM + `AddAdditionalAssemblies` del proyecto Client)
- [X] T018 Create `src/Admin/QuizArena.Admin/Auth/PersistingAuthenticationStateProvider.cs` y registrar `AddCascadingAuthenticationState()` + `AddRazorComponents().AddInteractiveServerComponents().AddInteractiveWebAssemblyComponents().AddAuthenticationStateSerialization(o => o.SerializeAllClaims = true)` en Program.cs
- [X] T019 Create `src/Admin/QuizArena.Admin.Client/Auth/PersistentAuthenticationStateProvider.cs` y wire `src/Admin/QuizArena.Admin.Client/Program.cs`: `AddAuthorizationCore()` + `AddCascadingAuthenticationState()` + `AddAuthenticationStateDeserialization()` + registro del provider
- [X] T020 Create `src/Admin/QuizArena.Admin/Services/BearerTokenHandler.cs` (DelegatingHandler que adjunta `Bearer {access_token}` vía `IHttpContextAccessor`) + helper de registro server-side `AddAdminServerServices()` en `src/Admin/QuizArena.Admin/Services/ServiceCollectionExtensions.cs`: `AddHttpClient` base `http://oroclash-api` con el handler (research R1 punto 3)
- [X] T021 Create mapeo compartido de errores `src/Admin/QuizArena.Admin.Client/Services/ApiResponseExtensions.cs`: `ProblemDetails` (RFC 7807) → `ApiErrorException(ApiErrorView)` incluyendo `FieldErrors`; helper `SendAsync` reutilizable por implementaciones Client y Server (data-model §4, FR-031)

### Shell UI + Design System (SPEC-016 screens/admin-shell.md)

- [X] T022 Update `src/Admin/QuizArena.Admin/Components/App.razor`: `<html data-theme="administration">`, `<link href="design-tokens.css">`, imports de fuentes Fira Sans/Code (Google Fonts según design-system/tokens/typography.md), `HeadOutlet`/routes
- [X] T023 Create `src/Admin/QuizArena.Admin/Components/Routes.razor`: `AuthorizeRouteView` con `NotAuthorized` → AccessDenied, handling de `must_change_password` (redirección a `{Identity:Authority}/Account/ChangePassword`), `NotFound` (contrato oidc-config §5)
- [X] T024 Create layout en `src/Admin/QuizArena.Admin/Components/Layout/MainLayout.razor` y `NavMenu.razor` (+ `.razor.css`): sidebar 240px colapsable/drawer según breakpoint, topbar, 10 secciones con iconos Lucide (SVG, no emoji), filtrado por claim `roles` (ADMIN 10, GAME_MANAGER 8 sin Rewards/Audit, REWARD_MANAGER 3), skip-link, landmarks (design-system/screens/admin-shell.md)
- [X] T025 [P] Create componentes compartidos lote 1 en `src/Admin/QuizArena.Admin/Components/Shared/`: `QuizButton.razor`, `QuizBadge.razor`, `QuizCard.razor`, `QuizTabs.razor` según `design-system/components/{button,badge,card,tabs}.md` (variants, states, a11y, tokens `var(--*)` exclusivamente)
- [X] T026 [P] Create componentes compartidos lote 2 en `src/Admin/QuizArena.Admin/Components/Shared/`: `QuizInput.razor`, `QuizSelect.razor`, `QuizModal.razor`, `QuizToast.razor` según `design-system/components/{input,select,modal,toast}.md` (label+aria-describedby, focus trap en modal, aria-live en toast)
- [X] T027 [P] Create componentes compartidos lote 3 en `src/Admin/QuizArena.Admin/Components/Shared/`: `QuizTable.razor` (dense/comfortable, sticky header, paginación, sortable aria-sort, cards@375), `QuizDrawer.razor`, y componentes de estado `LoadingSkeleton.razor`, `EmptyState.razor`, `ErrorState.razor` según `design-system/components/{table,drawer}.md` + `design-system/states.md`
- [X] T028 Update `src/Admin/QuizArena.Admin/Components/_Imports.razor` y crear `src/Admin/QuizArena.Admin.Client/_Imports.razor` con namespaces compartidos (Components.Shared, Models, Services, auth)
- [X] T029 Smoke test de la fase: `dotnet build` + arrancar AppHost → la app redirige a OroIdentityServer en `/connect/authorize`, callbacks `/signin-oidc` configurados, `/health` responde (quickstart Scenario 1 parcial)

**Checkpoint**: Contratos + auth + BFF + shell listos — las user stories pueden proceder en paralelo.

---

## Phase 3: User Story 1 — Acceso seguro y navegación administrativa (Priority: P1) 🎯 MVP

**Goal**: Usuario autorizado inicia sesión vía OroIdentityServer, aterriza en Dashboard, navega las 10 secciones; roles ven solo sus secciones; sesión expirada y `must_change_password` manejados (FR-001..006, spec US1).

**Independent Test**: Login con cuenta ADMIN → Dashboard → navegar 10 secciones (stubs); cuenta GAME_MANAGER/REWARD_MANAGER ve solo sus secciones; sin sesión → redirección al proveedor (quickstart Scenario 2).

### Implementation for User Story 1

- [X] T030 [US1] Create páginas stub de las 10 secciones en `src/Admin/QuizArena.Admin/Components/Pages/` (`Dashboard.razor`, `Games.razor`, `GameConfiguration.razor`, `Categories.razor`, `QuestionBank.razor`, `Players.razor`, `Rewards.razor`, `LiveGames.razor`, `Reports.razor`, `Audit.razor`): rutas `/admin/...`, `[Authorize]` con política por sección (Audit `AdminOnly`, Rewards `RewardManagerOrAdmin`, resto `AdminOrGameManager`), título + placeholder con estados de carga; completar registro de rutas del NavMenu (T024) contra estas páginas
- [X] T031 [US1] Create `src/Admin/QuizArena.Admin/Components/Pages/AccessDenied.razor` y `NotFound.razor` (denegación clara sin fuga de datos, CTA volver al Dashboard; FR-008 spec)
- [X] T032 [US1] Implementar manejo de sesión expirada en `src/Admin/QuizArena.Admin.Client/Services/SessionExpiredHandler.cs`: interceptor de respuestas 401 en HttpClient cliente → redirección a `/authentication/login`; en server, challenge OIDC automático; verificar flujo de logout (`/authentication/logout`)
- [X] T033 [US1] Implementar gating `must_change_password` en `src/Admin/QuizArena.Admin/Components/Routes.razor` + `AdminUserState` (claim detectado → bloqueo de navegación y redirección al flujo del proveedor con return URL; FR-004)
- [X] T034 [US1] Create proyecto de tests `tests/QuizArena.Admin.Tests/QuizArena.Admin.Tests.csproj` (xUnit, CPM, referencias a ambos proyectos admin) y tests de US1: filtrado de secciones por rol (lógica del NavMenu como clase testeable) y deserialización de `AdminUserState` desde claims en `tests/QuizArena.Admin.Tests/NavigationAndAuthTests.cs`
- [X] T035 [US1] Create test de arquitectura BFF en `tests/OroQuizClash.Architecture.Tests/AdminBffTests.cs`: (a) `src/Admin/**` no contiene referencias EF Core/DbContext/ADO.NET (extiende `DesignSystemNoDirectDbTests`), (b) el proyecto Client no contiene URLs absolutas al API (solo rutas relativas `/bff`/`/hubs`), (c) el proyecto Client no referencia paquetes de autenticación con tokens (SC-003, FR-030)

**Checkpoint**: US1 demostrable — login OIDC + 10 secciones navegables por rol; MVP esqueleto completo.

---

## Phase 4: User Story 2 — Administración de juegos y configuración (Priority: P1)

**Goal**: Crear/listar/editar juegos con configuración completa (12 campos), acciones de ciclo de vida con confirmación, edición bloqueada en juegos activos (FR-011..015, spec US2).

**Independent Test**: Crear juego con 12 campos válidos → aparece en listado; campos inválidos → errores inline; iniciar juego → configuración bloqueada; filtros/paginación funcionan (quickstart Scenario 4, SC-001 <3 min).

### Implementation for User Story 2

- [X] T036 [P] [US2] Create `src/Admin/QuizArena.Admin.Client/Services/ClientGamesAdminService.cs`: implementación de `IGamesAdminService` vía HttpClient a rutas `/bff/games*` (list+filtros, create, update, start/cancel/finish/force-finish/open-lobby, leaderboard) usando `ApiResponseExtensions` (T021)
- [X] T037 [US2] Create `src/Admin/QuizArena.Admin/Services/ServerGamesAdminService.cs` (misma interfaz, HttpClient base `http://oroclash-api` con `BearerTokenHandler`) y registrar ambas implementaciones en DI (cliente en `QuizArena.Admin.Client/Program.cs`, server en `ServiceCollectionExtensions.cs` T020) — contrato de comportamiento idéntico (contracts/service-interfaces.md)
- [X] T038 [US2] Create página `src/Admin/QuizArena.Admin/Components/Pages/Games.razor`: listado con `QuizTable` dense, filtros estado/categoría/búsqueda, paginación, estados Loading/Ready/Empty/Error, acciones por fila según `GameStatusView` (design-system/screens/admin-dashboard.md patrones + spec US2)
- [X] T039 [US2] Create página `src/Admin/QuizArena.Admin/Components/Pages/GameConfiguration.razor`: formulario de 12 campos con `QuizInput`/`QuizSelect`, validación inline por campo (rangos data-model §1: Rounds 1–10, TimeLimit ≥5s, Min≤Max players, montos ≥0), guardado draft/public, errores de negocio del API como mensaje de formulario; layout label-left@1024+ según design-system/pages/game-configuration.md
- [X] T040 [US2] Create página `src/Admin/QuizArena.Admin/Components/Pages/GameDetail.razor`: detalle con rondas + leaderboard público (`LeaderboardEntry`), acciones de ciclo de vida con `QuizModal` de confirmación explícita e impacto (cancel/finish/force-finish con razón), bloqueo de edición cuando `Status != Configuring` con explicación (FR-013/014, SC-011)
- [X] T041 [US2] Create tests en `tests/QuizArena.Admin.Tests/GamesServiceTests.cs`: validaciones de `GameConfigurationForm` (todos los rangos) y mapeo de rutas/verbos de `ClientGamesAdminService` con `HttpMessageHandler` fake (verifica llamadas a `/bff/games*` y mapeo ProblemDetails→ApiErrorException)

**Checkpoint**: US2 demostrable — operador crea y administra juegos de punta a punta sin tocar backend directamente.

---

## Phase 5: User Story 3 — Curación de contenido: categorías y preguntas (Priority: P1)

**Goal**: CRUD + ciclo de vida de categorías (gate de publicación con feedback) y preguntas (4 opciones/1 correcta, solo-lectura si en uso) (FR-016..019, spec US3).

**Independent Test**: Crear categoría + preguntas válidas → publicar con gate OK; intento sin gate → mensaje con faltantes; pregunta con opciones inválidas → bloqueo inline (quickstart Scenario 4, SC-004).

### Implementation for User Story 3

- [X] T042 [P] [US3] Create `src/Admin/QuizArena.Admin.Client/Services/ClientCategoriesService.cs` y `ClientQuestionsService.cs`: implementaciones vía `/bff/categories*` y `/bff/questions*` (CRUD + activate/deactivate/publish/archive)
- [X] T043 [US3] Create `src/Admin/QuizArena.Admin/Services/ServerCategoriesService.cs` y `ServerQuestionsService.cs` + registro DI dual (mismo contrato que T037)
- [X] T044 [US3] Create página `src/Admin/QuizArena.Admin/Components/Pages/Categories.razor`: listado filtrable/paginado, formulario (drawer@1024 / sheet@375) con atributos data-model §1 (área, nivel, edad min≤max, dificultad, tags ≤10), transiciones con confirmación, feedback de gate fallido (`ApiErrorView` con preguntas válidas faltantes), estados Loading/Ready/Empty/Error (design-system/screens/categories.md)
- [X] T045 [US3] Create página `src/Admin/QuizArena.Admin/Components/Pages/QuestionBank.razor`: listado + editor en `QuizDrawer` (enunciado, categoría, dificultad, exactamente 4 opciones con radio de correcta, explicación), validación inline (4 opciones/1 correcta/enunciado ≥10), modo solo-lectura cuando `InUseByLiveGame` con Badge "En uso", filtros por texto/categoría/dificultad/estado (design-system/screens/question-bank.md, FR-018/019)
- [X] T046 [US3] Create tests en `tests/QuizArena.Admin.Tests/ContentServiceTests.cs`: invariantes de `QuestionForm` (exactamente 4 opciones, exactamente 1 correcta), validaciones de `CategoryForm` (edad, tags), y mapeo del error de gate de publicación a mensaje accionable

**Checkpoint**: US3 demostrable — catálogo de contenido curado y publicable con gates del backend visibles para el curador.

---

## Phase 6: User Story 4 — Monitoreo en vivo y jugadores (Priority: P2)

**Goal**: Live Games con actualización automática vía SignalR reenviado por el BFF, estado de conexión, y consulta de jugadores (solo datos públicos) (FR-020..023, spec US4).

**Independent Test**: Con juego activo, Live Games refleja cambios (ronda, fin) en <5s sin refresh; corte de red → Reconnecting → resincronización REST; detalle de jugador sin respuestas individuales (quickstart Scenario 5, SC-005).

### Implementation for User Story 4

- [X] T047 [P] [US4] Create `src/Admin/QuizArena.Admin.Client/Services/ClientLiveGamesService.cs` y `ClientPlayersService.cs`: snapshot REST vía `/bff/games` + suscripción SignalR `HubConnectionBuilder().WithUrl("/hubs/game").WithAutomaticReconnect()` (sin accessTokenFactory — la cookie viaja en el handshake, contrato realtime §1)
- [X] T048 [US4] Create `src/Admin/QuizArena.Admin/Services/ServerLiveGamesService.cs` y `ServerPlayersService.cs` + registro DI dual; la suscripción server usa `HubConnection` contra `http://oroclash-api/hubs/game` con `BearerTokenHandler`/access token del HttpContext
- [X] T049 [US4] Implementar `LiveGameSubscription` en `src/Admin/QuizArena.Admin.Client/Services/LiveGameSubscription.cs`: `JoinGameGroup(gameId)`, filtro de eventos (atiende `GameStarted`, `PlayerJoined`, `RoundStarted`, `RoundCompleted`, `GameFinished`, `LeaderboardUpdated`; IGNORA `QuestionPresented`, `PlayerAnswered`, `ScoreUpdated` por privacidad), `ConnectionState` (Connected/Reconnecting/Disconnected), política Server Truth: tras reconexión re-consulta REST completa antes de mostrar datos en vivo (contracts/realtime.md §2/§3)
- [X] T050 [US4] Create página `src/Admin/QuizArena.Admin/Components/Pages/LiveGames.razor`: tabla comfortable con juegos activos (jugadores activos/total, ronda x/y, estado, started at), actualización en vivo por evento (fade 200), banner de estado de conexión con `aria-busy` y inputs deshabilitados durante Reconnecting, acción detener-juego con `QuizModal` de confirmación con impacto (jugadores activos), detalle expandible con leaderboard agregado (design-system/screens/live-games.md, FR-020/022/023)
- [X] T051 [US4] Create página `src/Admin/QuizArena.Admin/Components/Pages/Players.razor`: vista por juego (selección de juego activo/recente → jugadores con estado JOINED/PLAYING/WITHDRAWN/ELIMINATED/FINISHED y puntos asegurados), detalle de jugador con `ConsolationHistoryEntry[]`; sin respuestas individuales ni datos privados (data-model §1 Players, FR-021/022)
- [X] T052 [US4] Create tests en `tests/QuizArena.Admin.Tests/LiveGamesTests.cs`: filtro de eventos (los 3 privados ignorados), transiciones de `ConnectionState`, y verificación de que la reconexión dispara re-consulta REST (con hub/fakes)

**Checkpoint**: US4 demostrable — supervisor ve juegos en vivo actualizándose sin refresh y consulta jugadores sin violar privacidad.

---

## Phase 7: User Story 5 — Recompensas y redenciones (Priority: P2)

**Goal**: Catálogo de recompensas (CRUD + activar/desactivar) y procesamiento de redenciones (aprobar/rechazar/cancelar/entregar) con historial (FR-024..026, spec US5).

**Independent Test**: Crear/activar recompensa; procesar redención pendiente aprobar→entregar y otra rechazar; historial filtrable; sección denegada a GAME_MANAGER (quickstart Scenario 6).

### Implementation for User Story 5

- [X] T053 [P] [US5] Create `src/Admin/QuizArena.Admin.Client/Services/ClientRewardsService.cs` y `ClientRedemptionsService.cs`: vía `/bff/rewards*` y `/bff/redemptions*` (CRUD + activate/deactivate; list + approve/reject/cancel/deliver)
- [X] T054 [US5] Create `src/Admin/QuizArena.Admin/Services/ServerRewardsService.cs` y `ServerRedemptionsService.cs` + registro DI dual
- [X] T055 [US5] Create página `src/Admin/QuizArena.Admin/Components/Pages/Rewards.razor`: `QuizTabs` Catálogo/Redenciones; catálogo con formulario (nombre, descripción, costo en puntos >0, stock opcional) y activate/deactivate; redenciones con lista de pendientes (aprobar/rechazar/cancelar con confirmación) e historial filtrable por estado con paginación; Badges de estado icon+text; ruta protegida `RewardManagerOrAdmin` (data-model §1 Rewards, FR-024..026, SC-011)
- [X] T056 [US5] Create tests en `tests/QuizArena.Admin.Tests/RewardsTests.cs`: transiciones válidas de redención (Pending→Approved/Rejected/Cancelled, Approved→Delivered; terminales sin re-proceso) y denegación de rutas por rol

**Checkpoint**: US5 demostrable — ciclo completo de recompensas operable y auditado.

---

## Phase 8: User Story 6 — Dashboard, reportes y auditoría (Priority: P2)

**Goal**: Dashboard con KPIs reales, 6 tipos de reportes con período, y auditoría inmutable filtrable (FR-027..029, spec US6).

**Independent Test**: Dashboard coherente con el estado del sistema; cada tipo de reporte generable; auditoría filtrada por actor/acción/fecha sin opciones de mutación (quickstart Scenario 7).

### Implementation for User Story 6

- [X] T057 [P] [US6] Create `src/Admin/QuizArena.Admin.Client/Services/ClientDashboardService.cs`, `ClientReportsService.cs` y `ClientAuditService.cs`: vía `/bff/reports/*` y `/bff/audit*`
- [X] T058 [US6] Create `src/Admin/QuizArena.Admin/Services/ServerDashboardService.cs`, `ServerReportsService.cs`, `ServerAuditService.cs` + registro DI dual
- [X] T059 [US6] Create página `src/Admin/QuizArena.Admin/Components/Pages/Dashboard.razor`: KPI cards (`DashboardKpis`: juegos activos, jugadores, banco de preguntas, redenciones pendientes, recompensas pagadas), estados Loading (skeleton)/Ready/Empty/Error por widget con retry, atajos a secciones (design-system/screens/admin-dashboard.md)
- [X] T060 [US6] Create página `src/Admin/QuizArena.Admin/Components/Pages/Reports.razor`: selector de tipo de reporte (juego/categoría/pregunta/jugador/recompensas/leaderboard) + período cuando aplique, render tabular genérico de `ReportResult` con `QuizTable`, estado de procesamiento sin bloquear navegación, estados Empty por período sin datos (data-model §1 Reports, FR-028)
- [X] T061 [US6] Create página `src/Admin/QuizArena.Admin/Components/Pages/Audit.razor`: filtros actor/acción/rango de fechas, tabla comfortable ordenada cronológicamente con paginación, fila expandible con `DetailJson` en bloque de código, estrictamente solo-lectura (ninguna acción de mutación visible ni invocable), ruta `AdminOnly` (design-system/screens/audit.md, FR-029)
- [X] T062 [US6] Create tests en `tests/QuizArena.Admin.Tests/InsightsTests.cs`: mapeo de reportes a `ReportResult`, filtros de auditoría serializados correctamente a query string, y verificación (por reflexión del servicio) de que `IAuditService` no expone métodos de escritura

**Checkpoint**: US6 demostrable — visibilidad operativa completa (KPIs, reportes, auditoría inmutable).

---

## Phase 9: Polish & Cross-Cutting Concerns

**Purpose**: Calidad transversal, gates del Design System, configuración OIDC, validación quickstart y documentación.

- [X] T063 [P] Ejecutar gate de literales: `node .opencode/skills/design-system/scripts/validate-tokens.cjs --dir src/Admin` → 0 violaciones; corregir cualquier hex literal en componentes/páginas reemplazando por `var(--*)` (SPEC-016 GOVERNANCE CI)
- [X] T064 [P] Auditoría accesibilidad + responsive de las 10 secciones según `design-system/a11y.md` y `design-system/responsive.md`: contraste AA tema claro, navegación por teclado (tab order sidebar→filtros→contenido→paginación), foco visible, touch ≥44px, 0 scroll horizontal 375–1536, estados Loading/Ready/Empty/Error en todas las pantallas (SC-006/007)
- [X] T065 Create configuración y registro del cliente OIDC: `src/Admin/QuizArena.Admin/appsettings.json` (+ `Properties/launchSettings.json`) con `Identity:Authority`, `Identity:ClientId=quizarena-admin`, placeholder de client secret vía user-secrets/env; documentar/automatizar el registro del cliente confidencial en OroIdentityServer (redirect `/signin-oidc`, post-logout `/signout-callback-oidc`, scopes openid/profile/offline_access/roles + API scope) según contracts/oidc-config.md §1
- [X] T066 Ejecutar quickstart completo `specs/017-admin-application/quickstart.md` Scenarios 0–9 secuencialmente y archivar resultados en el propio quickstart (registro por escenario PASS/FAIL con evidencia)
- [X] T067 [P] Create `docs/adr/ADR-013-admin-bff-communication.md` (decisión: BFF YARP forwarder catch-all + interfaces compartidas dual-implementation + hub reenviado; alternativas rechazadas) y actualizar `README.md` con la sección Admin (arquitectura de comunicación, cómo arrancar con Aspire)
- [X] T068 Verificación final: `dotnet build OroQuizClash.slnx` + `dotnet test` (todos los proyectos de tests, incluidos `QuizArena.Admin.Tests` y `OroQuizClash.Architecture.Tests`) en verde; confirmar SC-003 (0 acceso DB) y gates del plan

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: sin dependencias — iniciar inmediatamente (T001 primero: todo lo demás necesita los proyectos).
- **Foundational (Phase 2)**: depende de Setup — **BLOQUEA todas las user stories** (contratos, auth OIDC, BFF, shell y componentes compartidos).
- **US1 (Phase 3)**: depende de Foundational — MVP esqueleto (login + navegación).
- **US2/US3 (Phases 4–5, P1)**: dependen de Foundational; pueden correr **en paralelo entre sí** (archivos distintos) y respecto de US1 salvo páginas stub (T030 crea stubs que luego US2/US3 reemplazan — ejecutar T030 antes).
- **US4/US5/US6 (Phases 6–8, P2)**: dependen de Foundational; independientes entre sí (paralelizables); US4 se beneficia de US2 (juegos activos para probar live) pero no bloquea.
- **Polish (Phase 9)**: depende de todas las stories (mínimo P1: US1+US2+US3) para validación completa.

### User Story Dependencies

- **US1 (P1)**: Foundational únicamente. Independent Test: login + 10 secciones + roles.
- **US2 (P1)**: Foundational + stubs de T030. Independent Test: crear/administrar juego completo.
- **US3 (P1)**: Foundational + stubs de T030. Independent Test: publicar categoría con gate.
- **US4 (P2)**: Foundational + stubs. Ideal con US2 para datos en vivo. Independent Test: live <5s + reconexión.
- **US5 (P2)**: Foundational + stubs. Independent Test: redención punta a punta.
- **US6 (P2)**: Foundational + stubs. Independent Test: KPIs/reportes/auditoría.

### Within Each User Story

1. Servicios cliente [P] → servicios server + DI → páginas → tests.
2. Páginas consumen componentes compartidos de Foundational (nunca crear componentes duplicados).
3. Tests al final de cada story validan el incremento independiente.

### Parallel Opportunities

- T004 + T006 (csproj + assets) en paralelo dentro de Setup.
- T008–T013 (modelos + interfaces) todos [P] — archivos distintos.
- T025–T027 (componentes compartidos lotes 1–3) [P] — archivos distintos.
- US2/US3/US4/US5/US6: servicios cliente [P] al inicio de cada fase; las fases mismas paralelizables con 2+ desarrolladores tras Foundational.
- T063 + T064 + T067 [P] en Polish (archivos/ámbitos distintos).

---

## Parallel Example: Foundational (contratos)

```bash
# Modelos e interfaces en paralelo (archivos distintos):
Task: "Create modelos comunes en QuizArena.Admin.Client/Models/Common.cs"
Task: "Create modelos de juegos en QuizArena.Admin.Client/Models/GameModels.cs"
Task: "Create modelos de contenido en QuizArena.Admin.Client/Models/ContentModels.cs"
Task: "Create las 10 interfaces en QuizArena.Admin.Client/Services/"
```

## Parallel Example: User Stories tras Foundational

```bash
# Dos desarrolladores en paralelo (sin solapamiento de archivos):
Developer A - US2: Pages/Games.razor, Pages/GameConfiguration.razor, Services/*GamesAdminService.cs
Developer B - US3: Pages/Categories.razor, Pages/QuestionBank.razor, Services/*CategoriesService.cs, *QuestionsService.cs
```

---

## Implementation Strategy

### MVP First (Foundational + US1 + US2)

1. Completar Phase 1: Setup (proyecto net10.0 + slnx + CPM + AppHost)
2. Completar Phase 2: Foundational (contratos + OIDC + BFF + shell) — **BLOQUEANTE**
3. Completar Phase 3: US1 — login + navegación por rol (MVP esqueleto)
4. Completar Phase 4: US2 — administración de juegos — **MVP demo**: operador crea juego completo <3 min con validación y ciclo de vida
5. **STOP y VALIDAR**: quickstart Scenarios 0–4 + architecture tests
6. Demo si está listo — contenido/live/recompensas/reportes no se requieren para el MVP admin

### Incremental Delivery

1. Setup + Foundational → BFF operativo (llamadas `/bff/*` con token server-side)
2. + US1 → navegación segura por rol
3. + US2 → juegos operables (MVP)
4. + US3 → contenido curado (juegos consumibles)
5. + US4 → operación en vivo
6. + US5 → recompensas bajo control humano
7. + US6 → visibilidad completa
8. Polish → gates Design System + quickstart 0–9 + ADR/README
- Cada story añade valor sin romper previas (mismas interfaces `I*Service`, mismo BFF)

### Parallel Team Strategy

Con 3 desarrolladores tras Foundational:
1. Dev A: US1 → US6 (shell/dashboard/reportes/auditoría)
2. Dev B: US2 → US4 (juegos → live games)
3. Dev C: US3 → US5 (contenido → recompensas)
4. Polish conjunto (gates + quickstart)

---

## Notes

- [P] = archivos distintos, sin dependencias — seguro paralelizar
- [Story] = trazabilidad a spec.md (US1 acceso P1, US2 juegos P1, US3 contenido P1, US4 live P2, US5 recompensas P2, US6 visibilidad P2)
- **BFF innegociable**: ninguna llamada del navegador sale del origen propio; tokens solo en el servidor (contracts/bff-endpoints.md §5)
- **Server Truth**: tras evento realtime, re-consultar REST (contracts/realtime.md §3)
- net10.0 único en todos los proyectos/tests (mandato usuario; global.json 10.0.400)
- Componentes UI solo con tokens `var(--*)` del Design System SPEC-016 — gate `validate-tokens.cjs`
- Commit por tarea o grupo lógico; validar cada checkpoint antes de continuar
