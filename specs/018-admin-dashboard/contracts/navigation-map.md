# Contract: Navigation Map — Drill-down & Quick Actions

**Branch**: `018-admin-dashboard` | **Date**: 2026-08-28

Mapa de navegación del dashboard. No son nuevos endpoints BFF; son rutas Blazor existentes (SPEC-017) que el dashboard enlaza con contexto pre-filtrado. Todas requieren sesión válida; el destino impone la autorización final (403 si no autorizado — FR-011/014).

## 1. Drill-down de métricas (MetricTile → listado)

Cada `MetricValue.DrillDownRoute` apunta a estas rutas. `Count` de la tarjeta DEBE coincidir con `PagedResult.TotalCount` del destino (SC-003).

| MetricId | Label | Ruta destino | Filtro aplicado |
|----------|-------|--------------|-----------------|
| `ActiveGames` | Juegos activos | `/games?status=Active` y `/live` | `GameStatus IN (IN_PROGRESS, ROUND_IN_PROGRESS, ROUND_COMPLETED)` |
| `ScheduledGames` | Juegos programados | `/games?status=Scheduled` | `READY, WAITING_FOR_PLAYERS` |
| `FinishedGames` | Juegos finalizados | `/games?status=Finished` | `FINISHED, FORCED_FINISHED, CANCELLED` |
| `ConnectedPlayers` | Jugadores conectados | `/players?view=online` | presencia online |
| `ActivePlayers` | Jugadores activos | `/players?view=active` | `State=PLAYING` en `IN_PROGRESS` |
| `AvailableQuestions` | Preguntas disponibles | `/questions?status=Active` | `Active/Published` |
| `Categories` | Categorías | `/categories?status=Active` | `Active` |
| `Rewards` | Premios | `/rewards?status=Active` | `Active` |
| `Redemptions` | Canjes | `/redemptions?status=Pending` | `Pending` (o totales — `SourceLabel` aclara) |
| `GeneralStatistics` | Estadísticas generales | `/reports?focus=general` | agregados periodo |

**Fallback si no autorizado**: `DrillDownRoute == null` → tarjeta no clicable con mensaje "Sin permiso para ver el detalle" (FR-014). Si se accede por URL directa, el destino muestra página "Acceso denegado" (existente 017) sin fuga.

## 2. Quick Actions — 7 atajos (≤1 clic, FR-009)

Catálogo estático `QuickActionsCatalog.All` (dto en `QuizArena.Admin.Client/Services/QuickActionsCatalog.cs`).

| id | Label | Icon Lucide | Ruta destino | AllowedRoles | Descripción corta |
|----|-------|-------------|--------------|--------------|-------------------|
| `create-game` | Crear juego | `Plus` | `/games/new` | ADMIN, GAME_MANAGER | Crear un nuevo juego |
| `configure-game` | Configurar juego | `Settings2` | `/games?view=config` | ADMIN, GAME_MANAGER | Configurar juego existente |
| `manage-questions` | Gestionar preguntas | `FileQuestion` | `/questions` | ADMIN, GAME_MANAGER | Banco de preguntas |
| `view-active-games` | Ver juegos activos | `Activity` | `/games?status=Active` | ADMIN, GAME_MANAGER | Juegos en curso |
| `view-players` | Ver jugadores | `Users` | `/players` | ADMIN, GAME_MANAGER | Listado de jugadores |
| `manage-rewards` | Gestionar premios | `Gift` | `/rewards` | ADMIN, REWARD_MANAGER | Catálogo y canjes |
| `view-reports` | Consultar reportes | `BarChart3` | `/reports` | ADMIN, GAME_MANAGER, REWARD_MANAGER | Reportes y estadísticas |

**Reglas UI**:
- `QuickActionGrid` renderiza solo acciones donde `AllowedRoles` intersecta `User.Roles`; si no intersecta → oculto o `aria-disabled` con `title="Requiere rol X"` (FR-011).
- Cada `QuickActionCard` 44px mínimo, foco tras métricas (FR-012), keyboard `Enter` navega.
- ADMIN ve 7; GAME_MANAGER ve 6 (sin Gestionar premios); REWARD_MANAGER ve 2 (Gestionar premios + Consultar reportes).

## 3. Refresh bar

```
[Actualizar]  Última actualización: 12:00:45 UTC  [Auto-refresh: ON/OFF (45s)]
```

- `Actualizar` → `IDashboardService.GetSnapshotAsync()` sin recarga de página (FR-008).
- Auto-refresh 45s solo si `document.visibilityState === 'visible'`; pausa en `hidden`; stop en 401.
- Timestamp `GeneratedAt` del snapshot; `aria-live="polite"` anuncia cambios.

## 4. Matriz de autorización (resumen)

| Rol | Métricas visibles | Atajos visibles | Drill-down permitido |
|-----|-------------------|-----------------|----------------------|
| ADMIN | 10/10 | 7/7 | todos |
| GAME_MANAGER | 10/10 (premios/canjes visibles pero opcionalmente enmascarados si policy lo exige) | 6 (sin Gestionar premios) | todo excepto Rewards/Redemptions detail si REWARD_MANAGER-only |
| REWARD_MANAGER | al menos Premios/Canjes/Estadísticas; resto enmascarado o con mensaje permiso (US1 esc.5) | 2 (Gestionar premios, Consultar reportes) | Rewards/Redemptions/Reports; resto denegado |

Implementado vía `[Authorize(Policy=AdminPolicies.*)]` en destinos + filtro en `MetricsGrid`/`QuickActionGrid`.
