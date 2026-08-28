# Research: Admin Dashboard

**Branch**: `018-admin-dashboard` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El dashboard reutiliza 100% el patrón BFF/OIDC/Design System de 017 (research R1-R8); esta fase cierra las incógnitas propias de 018.

---

## R1. Estrategia de agregación de las 10 métricas — snapshot vs. fan-out cliente

**Decision**: `IDashboardService.GetSnapshotAsync()` único (server: `ServerDashboardService` → `http://oroclash-api/api/dashboard/snapshot` si existe; si no existe, fan-out server-side paralelo con `Task.WhenAll` sobre los endpoints ya existentes: `GET /api/games?status=...`, `/api/games/{id}/players`, `/api/questions`, `/api/categories`, `/api/rewards`, `/api/redemptions/pending`, `GET /api/reports/games|players` para estadísticas generales). El cliente WASM hace **1 llamada** `GET /bff/dashboard/snapshot` vía forwarder YARP; el servidor compone el snapshot y retorna `DashboardSnapshot` (timestamp + 10 bloques). Cada bloque lleva `State` (Ready/Empty/Error) y `Retryable` para reintento aislado.

**Rationale**: SC-001 (<2s carga, sin pantalla en blanco) y edge case "un bloque caído no bloquea los demás" exigen aislamiento por bloque. 1 llamada cliente reduce 10 round-trips navegador→server; la composición server-side aprovecha service discovery (latencia <5ms intra-cluster) y permite que el BFF mapee errores por bloque a `MetricState.Error` sin fallar el snapshot completo. Si el API ya expone `/api/dashboard/snapshot` (agregado precálculado) se consume directo; si no, el fan-out paralelo tiene el mismo shape de respuesta y es compatible sin cambiar contrato cliente.

**Alternatives considered**:
- 10 llamadas independientes desde el cliente (`ClientDashboardService` fan-out): rechazado — 10 handshakes BFF, waterfall, sin aislamiento centralizado; rompe SC-001.
- Endpoint nuevo obligatorio en el API antes de entregar UI: rechazado — acopla entrega a cambio backend; el fan-out server-side es fallback que preserva el contrato.
- SignalR push para métricas: rechazado para MVP — polling manual+auto es suficiente (SC-004 ≤30s/60s); push se evaluará post-MVP si hay requisito de latencia <5s.

---

## R2. Semántica Jugadores conectados vs. Jugadores activos

**Decision**: Dos métricas distinguibles con fuentes distintas; si el backend no distingue, aproximar con tooltip:
- **Conectados** → `GET /api/games/presence/online` o `GET /api/players/online` si existe; fallback = `Hub presence` (conteo de conexiones SignalR activas) o `GamePlayers` con `LastSeen > now-5m`. 
- **Activos** → `GamePlayers` con `State=PLAYING` en juegos `IN_PROGRESS` (`GET /api/games?status=InProgress` + conteo de jugadores).

Ambas tarjetas muestran `MetricValue { Count, SourceLabel, Tooltip }`; `Tooltip` explica la fuente cuando es aproximación (FR-003). Nunca se duplica el mismo valor de forma engañosa: si solo hay una fuente, una tarjeta muestra el valor y la otra muestra `N/A` con explicación o el mismo número con badge `aprox.`.

**Rationale**: FR-003 + Assumption "semántica de jugadores" permiten la mejor aproximación documentada. La decisión se codifica en `ServerDashboardService` y se expone en UI como ayuda accesible; evita bloquear el feature por falta de endpoint ideal.

**Alternatives considered**:
- Unificar en "Jugadores": rechazado — viola FR-003 que exige dos métricas distinguibles.
- Inventar distinción en el frontend: prohibido (Constitución V — server truth).

---

## R3. Catálogo de 7 accesos rápidos y matriz de autorización

**Decision**: Catálogo estático tipado `QuickActionsCatalog.All` en `QuizArena.Admin.Client` con 7 entradas:

| id | etiqueta | icono Lucide | ruta destino | roles |
|----|----------|--------------|--------------|-------|
| create-game | Crear juego | `Plus` | `/games/new` | ADMIN, GAME_MANAGER |
| configure-game | Configurar juego | `Settings2` | `/games?view=config` | ADMIN, GAME_MANAGER |
| manage-questions | Gestionar preguntas | `FileQuestion` | `/questions` | ADMIN, GAME_MANAGER |
| view-active-games | Ver juegos activos | `Activity` | `/games?status=Active` | ADMIN, GAME_MANAGER |
| view-players | Ver jugadores | `Users` | `/players` | ADMIN, GAME_MANAGER |
| manage-rewards | Gestionar premios | `Gift` | `/rewards` | ADMIN, REWARD_MANAGER |
| view-reports | Consultar reportes | `BarChart3` | `/reports` | ADMIN, GAME_MANAGER, REWARD_MANAGER |

Filtrado en UI por `AuthenticationState` claims `roles`; atajos no permitidos se ocultan o deshabilitan con `aria-disabled` + `title` explicativo (FR-011). Protección real en destino: rutas con `[Authorize(Policy=...)]` espejo de `SecurityPolicies`; acceso directo por URL retorna 403 sin fuga (SC-010). Iconos Lucide (prohibido emoji — FR-012); componente `QuickActionCard` 44px mínimo.

**Rationale**: FR-009/010 exigen ≤1 clic y contexto útil (filtros pre-aplicados via query string). La matriz refleja `SecurityPolicies.PolicyRoles` ya auditada en 017 research R8.

**Alternatives considered**:
- Generar atajos dinámicamente desde backend: rechazado — añade endpoint sin valor; el catálogo es estable y la autorización ya está en claims.
- Ocultar sin deshabilitar: ambas variantes válidas; se soportan ambas (hide para REWARD_MANAGER→game actions, disabled+reason como fallback accesible).

---

## R4. Drill-down — coherencia tarjeta → listado

**Decision**: Cada `MetricTile` es clicable (`<a>` o `button` con `NavigationManager.NavigateTo`) y apunta a la ruta canónica de su entidad con filtro que reproduce el conteo:

| métrica | destino |
|---------|---------|
| Juegos activos | `/games?status=Active` + `/live` |
| Juegos programados | `/games?status=Scheduled` (READY/WAITING) |
| Juegos finalizados | `/games?status=Finished` |
| Jugadores conectados/activos | `/players?view=online` / `/players?view=active` |
| Preguntas disponibles | `/questions?status=Active` |
| Categorías | `/categories?status=Active` |
| Premios | `/rewards?status=Active` |
| Canjes | `/redemptions?status=Pending` |
| Estadísticas generales | `/reports?focus=general` |

Coherencia verificada por SC-003: el `Count` de la tarjeta coincide con `PagedResult.TotalCount` del listado destino (mismo query server-side). Autorización preservada: si el usuario no tiene claim para el destino, `MetricTile` navega pero el destino muestra "Acceso denegado" (FR-014).

**Rationale**: P2 del spec; evita duplicar lógica de filtrado. Reusa listados existentes de 017 (no nuevas páginas).

**Alternatives considered**:
- Modal con detalle en el dashboard: rechazado — duplica vistas y no escala a 1000+ elementos.
- Navegación sin filtro: viola SC-003 (incoherencia).

---

## R5. Actualización sin recarga — manual + auto-refresh

**Decision**:
- **Botón Actualizar** siempre visible en `DashboardRefreshBar` (FR-008 obligatorio); dispara `GetSnapshotAsync()` y actualiza timestamp `GeneratedAt`.
- **Auto-refresh 30-60s** opcional (no bloqueante MVP): `PeriodicTimer` 45s en `Dashboard.razor` (InteractiveServer + WASM). Reglas:
  - Solo si `document.visibilityState === 'visible'` (JS interop `visibilitychange` → pausa/reanuda).
  - Se detiene en 401 (sesión expirada) y muestra banner "Sesión expirada — re-autenticar" sin reintento en bucle (edge case).
  - Durante carga, skeletons por bloque; error de un bloque no cancela el timer.
- **Polling server-side**: `CancellationToken` por bloque con timeout 5s; >5s → `MetricState.Loading` con progress accesible (edge case).

**Rationale**: FR-008 + SC-004 (≤30s manual, ≤60s auto). `PeriodicTimer` es nativo net10 y evita `Task.Delay` leakage; visibilidad evita tráfico en pestaña oculta.

**Alternatives considered**:
- SignalR push para actualizaciones en vivo: evaluado como mejora post-MVP; no requerido para SC-004 y añade dependencia de hub.
- `setInterval` JS puro: rechazado — componente Blazor gestiona lifecycle/dispose correctamente con `PeriodicTimer` + `IAsyncDisposable`.

---

## R6. Responsive, accesibilidad y estados por bloque

**Decision**:
- **Grilla**: `MetricsGrid` CSS grid `1 col <640px`, `2 cols 640-1024`, `3 cols 1024-1440`, `4 cols ≥1440`; sin scroll horizontal 375-1536 (SC-006).
- **Estados por bloque** (FR-007, SC-009): `Loading` (skeleton SPEC-016 `Skeleton` component), `Empty` (0 con texto sugerido + CTA), `Ready` (valor + `aria-live="polite"`), `Error` (mensaje accionable + botón Reintentar aislado que re-llama solo ese métrico via `GetMetricAsync(metricId)`).
- **QuickActionGrid**: `2 cols <640`, `3 cols ≥768`, `4 cols ≥1024`; tarjetas 44px, foco tras métricas.
- **WCAG 2.2 AA**: tema `administration` (contraste ya auditado SPEC-016 §19); foco visible, navegación teclado, `role="status"` por métrica, `aria-busy` en loading.
- **Tokens**: sin literales hex; `validate-tokens.cjs --dir src/Admin` gate.

**Rationale**: Directo de SPEC-016 responsive.md §7/§11, a11y.md §19, states.md §9 + SC-006/007/009. Aislamiento por bloque satisface "un bloque caído no bloquea los demás".

**Alternatives considered**:
- Masonry/flex sin grid: rechazado — pierde alineación y control de columnas.
- Un solo estado global de carga para todo el dashboard: viola FR-007 y SC-009.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | Snapshot único `/bff/dashboard/snapshot` con fan-out server-side paralelo | FR-007/008, SC-001/SC-009 |
| 2 | Conectados vs Activos con fuentes distinguibles + tooltip aprox. | FR-003, Assumption |
| 3 | Catálogo 7 atajos Lucide + matriz 3 roles + políticas espejo | FR-009/011, SC-002/010 |
| 4 | Drill-down a rutas existentes con filtro coherente | FR-013/014, SC-003 |
| 5 | Actualizar manual + PeriodicTimer 45s con pausa visibilidad + stop en 401 | FR-008, SC-004, edge 401 |
| 6 | MetricsGrid/QuickActionGrid responsive + 4 estados por bloque + AA | FR-007/012, SC-006/007/009, SPEC-016 |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
