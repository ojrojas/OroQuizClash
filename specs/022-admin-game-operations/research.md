# Research: Admin Game Operations

**Branch**: `022-admin-game-operations` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Todos los NEEDS CLARIFICATION resueltos. El feature reutiliza `Game`/`GameRound`/`PointTransaction` de `001`/`005`/`012` y patrón BFF/OIDC/SignalR/Design System de 017–021; esta fase cierra las incógnitas propias de 022.

---

## R1. Mapeo de 10 indicadores a fuentes de dominio (Server Truth)

**Decision**: Los 10 indicadores son proyecciones de lectura, no cálculos en UI (Constitución V):

| Indicador spec | Fuente dominio/BFF | Nota |
|----------------|-------------------|------|
| `Game Status` | `Game.Status` (`DRAFT`–`IN_PROGRESS`–`FINISHED` + `IsPaused` flag) → `GameStateView` admin (8 estados 019) | badge + tooltip mapeo |
| `Current Round` | `Game.CurrentRound` / `GameRound.RoundNumber` (0 si no iniciado) | 0 para `Draft`/`Configured` |
| `Current Question` | `GameRound.CurrentQuestionId` → `GET /api/games/{id}/questions/current` (4 opciones A–D, sin revelar `IsCorrect` salvo política) | skeleton si tarda >5s |
| `Players` | `COUNT(GamePlayer WHERE GameId=@id)` | total inscritos |
| `Players Connected` | `COUNT(GamePlayer WHERE LastSeen > now-2m)` o `Hub UserSession` presence | presencia online, tooltip si es aproximación |
| `Players Answered` | `COUNT(GamePlayer WHERE LastAnswerSubmittedAt == CurrentQuestionId)` | respuesta válida para pregunta actual |
| `Players Waiting` | `Players Connected − Players Answered` (derivado server-side, no en UI) | coherencia server-side |
| `Scores` | `PointTransaction` ledger → `GET /api/games/{id}/leaderboard` (`PlayerId`/`DisplayName`/`Score`/`SecuredPoints`) | reconstruible, no cálculo en UI |
| `Current Level` | `Game.CurrentLevel` derivado de `DifficultyStrategy` (1–5) | nivel de progresión actual |
| `Game Timer` | `TimePerQuestion − (now − StartedAt)` server-side, congelado en `Paused` | sincronizado con servidor, no `DateTime.Now` cliente |

Cada indicador lleva `RowVersion` para coherencia y se expone como `LiveGameView` DTO.

**Rationale**: Constitución V (Server Truth) y D (ledger) — el cliente es presentación, no autoridad. Mapeo evita inventar valores y garantiza que `Scores` coincida con `leaderboard`.

**Alternatives considered**:
- Calcular `Scores` en UI a partir de respuestas: rechazado — viola V y D.
- Usar `DateTime.Now` del cliente para `Game Timer`: rechazado — reloj del cliente no es autoridad, se congela mal en `Paused`.

---

## R2. Estrategia de actualización en vivo — polling vs WebSocket (BFF)

**Decision**: Híbrido con preferencia por push, fallback a polling:

- **Primario**: WebSocket via BFF `MapForwarder("/hubs/game")` ya existente (017) que proxy WebSockets de `oroclash-api` ( `GameHub` con `Group("game-{id}")`). El operador se une a `Groups.AddToGroupAsync(Context.ConnectionId, $"game-{gameId}")` server-side; el hub emite `QuestionAvailable`, `ScoreUpdated`, `RoundCompleted`, `GamePaused`, `GameFinished` etc. El cliente WASM usa `HubConnection` con `BaseAddress = "/hubs/game"` (misma origen, cookie viaja, forwarder adjunta Bearer).
- **Fallback**: Polling `PeriodicTimer` 3–5s (`GET /bff/games/{id}/live` o `GET /bff/games/{id}` + `leaderboard` + `players`) si `HubConnection.State != Connected` o `WebSocket` no disponible. `Polling` respeta `visibilityState` (pausa si pestaña oculta) y se detiene en 401.
- **Reconciliación**: Tras reconexión o evento, la UI re-consulta `GET /bff/games/{id}` + `leaderboard` para asegurar coherencia (no confiar solo en evento).

**Rationale**: `LiveGamesService` ya usa `MapForwarder` para hub (017/018). Reutilizarlo evita duplicar hub y mantiene BFF (token nunca en navegador). Polling 3–5s cumple SC-002 (≤3s) incluso sin WebSocket, y  polling + push es la estrategia de `012-realtime-game-events` (best-effort, no source of truth).

**Alternatives considered**:
- Solo polling: rechazado — latencia 3–5s es aceptable pero push reduce a <1s cuando está disponible.
- Solo WebSocket sin fallback: rechazado — si el hub cae, la vista quedaría obsoleta.
- WASM → hub directo con `accessTokenFactory`: rechazado — expone JWT al navegador (rompe BFF).

---

## R3. Modelo de 4 acciones controladas con `RowVersion` + `IdempotencyKey`

**Decision**: 4 comandos dedicados, cada uno con confirmación UI + `RowVersion` + `IdempotencyKey` (UUID v4 generado por cliente por intento):

| Acción | Transición | `RowVersion` | `IdempotencyKey` | Confirmación |
|--------|------------|--------------|------------------|--------------|
| `Pause` | `Running → Paused` | `If-Match: W/"{RowVersion}"` | `X-Idempotency-Key: {uuid}` | "¿Pausar partida? Se congelará el timer y se bloquearán respuestas." |
| `Resume` | `Paused → Running` | `If-Match` | `X-Idempotency-Key` | "¿Reanudar partida?" |
| `Cancel` | `* → Cancelled` (desde `Running`/`Paused`/`Ready`/`Scheduled`) | `If-Match` | `X-Idempotency-Key` + `Reason?` | "¿Cancelar partida? Acción terminal." |
| `Force Finish` | `Running`/`Paused → Finished` (forzado) | `If-Match` | `X-IdempotencyKey` + `privileged:true` | "¿Forzar finalización? Acción privilegiada, auditable." |

Transiciones inválidas (`Finished → Pause`, `Draft → Force Finish`) → `422 InvalidGameState`. Concurrencia: `409 ConcurrencyConflict` si `RowVersion` desactualizado. Idempotencia: segundo intento con mismo `IdempotencyKey` no muta ni duplica auditoría (server almacena `IdempotencyKey` en `Outbox`/`Audit`).

**Rationale**: Constitución F (optimistic concurrency + idempotencia) y I (auditoría). `RowVersion` ya protege `Game` en 001; `IdempotencyKey` evita doble auditoría por reintento de red (edge case).

**Alternatives considered**:
- Sin `IdempotencyKey`: rechazado — reintento por falta de confirmación duplicaría auditoría.
- Sin `RowVersion`: rechazado — dos operadores pausando simultáneamente crearían dos transiciones.

---

## R4. Auditoría append-only para operaciones privilegiadas (Constitución I)

**Decision**: Cada operación exitosa genera `GameAuditEntry` via `Outbox` en `SaveChanges` (mismo patrón que 019):

- `GameAuditEntry { GameId, ActorId (sub de JWT), Timestamp UTC, FromState, ToState, Action (Pause/Resume/Cancel/ForceFinish), Reason?, CorrelationId, Result, IdempotencyKey }`
- Append-only, no muta historial; intentos fallidos no generan auditoría de éxito (solo log de error).
- `Force Finish` marca `privileged:true` y `Reason` opcional.
- Expuesto como `GET /api/games/{id}/audit?from=&to=` (si existe) o via `GET /bff/games/{id}` con `history` embebido.

**Rationale**: El spec exige "Las operaciones privilegiadas deberán quedar registradas mediante auditoría." Reutiliza `BuildingBlocks.EventBus` Outbox y `AppDbContextBase` ya existentes (017).

**Alternatives considered**:
- Log solo en cliente: rechazado — no es auditable server-side.
- Tabla separada sin Outbox: rechazado — no es transaccional con `Game` (riesgo de auditoría sin transición).

---

## R5. Sincronización de `Game Timer` y manejo de `Paused`

**Decision**:
- `Game Timer` es `TimePerQuestion − (now − StartedAt)` calculado server-side en `GET /bff/games/{id}` (campo `remainingSeconds`). En `Paused`, el servidor congela `StartedAt` (o guarda `PausedAt` y resta `PausedDuration`).
- UI: `LiveGameHeader.razor` con `PeriodicTimer` 1s que decrementa localmente pero se re-sincroniza cada 3–5s con el servidor (polling/WebSocket) y en cada evento `ScoreUpdated`/`RoundCompleted`. Si `remainingSeconds` del servidor difiere >2s del local, se corrige.
- En `Paused`, el timer muestra "Pausado" y no decrementa.

**Rationale**: Constitución V (server timestamps son autoridad). Usar `DateTime.UtcNow` del cliente como autoridad rompería la pausa y la sincronización.

**Alternatives considered**:
- Timer solo en cliente con `setInterval`: rechazado — se desincroniza y no se congela correctamente en `Paused`.
- Timer solo server-side con push cada segundo: rechazado — chatty, overhead innecesario.

---

## R6. Responsive, a11y y estados por indicador (Design System)

**Decision**:
- Grilla `LiveGame` stack vertical en <640px (1 col), 2 cols 640–1024, 3 cols 1024–1440, con 10 indicadores apilados; sin scroll horizontal 375–1536 (SC-009).
- Cada indicador con estados `Loading` (skeleton), `Ready`, `Empty` (0 jugadores), `Error` con Reintentar aislado (no bloquea los demás).
- `aria-live="polite"` para `Scores`/`Players Answered`/`Game Timer`, `role="status"` por indicador, `aria-busy` en carga.
- Botones de acción 44px mínimo, deshabilitados con razón si estado no permite la transición (`Finished → Pause` deshabilitado con tooltip "Juego finalizado").

**Rationale**: SPEC-016 `responsive.md` + `a11y.md` + SC-009. Aislamiento por indicador ya usado en 018 (dashboard) y se reutiliza.

**Alternatives considered**:
- Un solo estado global de carga para toda la vista: rechazado — bloquea la vista si un indicador falla.

---

## Consolidated Decisions Summary

| # | Decisión | Fuente |
|---|----------|--------|
| 1 | 10 indicadores mapeados a `Game`/`GameRound`/`PointTransaction`/`GamePlayer` con `ValidQuestionCount` | FR-001..006, Constitución V/D |
| 2 | Híbrido WebSocket via BFF forwarder + polling 3–5s fallback | FR-007, 012-realtime, SC-002 |
| 3 | 4 acciones con `RowVersion` + `IdempotencyKey` + confirmación | FR-010/011/019, Constitución F |
| 4 | Auditoría append-only via Outbox con `privileged` para `Force Finish` | FR-013, Constitución I |
| 5 | `Game Timer` server-side `remainingSeconds` + re-sync 3–5s + pausa | FR-006, V |
| 6 | Stack responsive + 4 estados por indicador + AA | FR-009, SC-009, SPEC-016 |

Sin NEEDS CLARIFICATION pendientes. Listo para Phase 1.
