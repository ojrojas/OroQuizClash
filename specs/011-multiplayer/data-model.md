# Data Model: Multiplayer (SPEC-011)

**Date**: 2026-08-27 | **Plan**: [plan.md](plan.md) | **Research**: [research.md](research.md)

El modelo multiplayer extiende el agregado `Game` existente. Sin nuevos agregados: `GamePlayer` gana un campo, el resto son derivaciones, extensiones de consulta y un read-model de leaderboard. Leyenda: **EXISTING** (sin cambios), **EXTEND** (modificado por este SPEC), **NEW** (creado por este SPEC), **DERIVED** (calculado, no persistido).

## Entidades

### Game (AggregateRoot&lt;GameId&gt;) — EXISTING (comportamiento EXTEND)

Límite de consistencia del multiplayer. Contiene `Players`, `Rounds`, `Answers`, `PointTransactions`.

| Campo | Tipo | Estado | Notas |
|-------|------|--------|-------|
| `Id` | `GameId` | EXISTING | StronglyTypedId&lt;Guid&gt; |
| `Status` | `GameStatus` | EXISTING | Gobierna cuándo se permite unirse/responder |
| `RowVersion` | `byte[]` | EXISTING | Token de concurrencia optimista del agregado — protege TODAS las mutaciones de estado de jugador (decisión R3) |
| `Players` | `IReadOnlyList<GamePlayer>` | EXISTING | Participaciones del juego |

Comportamiento nuevo/extendido:

- `StartRound(...)` — EXTEND: además de crear `GameRound`, avanza `CurrentRoundNumber` de todos los jugadores `Active` al nuevo `RoundNumber` (FR-010).
- `GetPlayerAnswerState(Guid playerId)` — NEW: retorna `AnswerStatus` del jugador para la ronda actual (DERIVED de `Answers`; decisión R2).
- `SubmitAnswer(...)`, `WithdrawPlayer(...)`, `EliminatePlayer(...)` — EXISTING: ya congelan implícitamente la participación; con R1, la congelación de `CurrentRoundNumber` ocurre en `MarkWithdrawn`/`MarkEliminated`.

### GamePlayer (Entity&lt;GamePlayerId&gt; dentro de Game) — EXTEND

Estado individual de participación exigido por FR-001.

| Campo | Tipo | Estado | Reglas de validación |
|-------|------|--------|----------------------|
| `Id` | `GamePlayerId` | EXISTING | StronglyTypedId&lt;Guid&gt; |
| `GameId` | `GameId` | EXISTING | FK al juego; único por `(GameId, UserId)` (índice DB) |
| `UserId` | `Guid` | EXISTING | = `PlayerId` del spec; claim `sub` de OroIdentityServer |
| `DisplayName` | `string?` | EXISTING | Opcional, mostrado en leaderboard |
| `JoinedAt` | `DateTimeOffset` | EXISTING | Estabilidad final del desempate del ranking |
| `ParticipationStatus` | `PlayerParticipationStatus` | EXISTING | = `Status` del spec (FR-002) |
| `Score` | `PlayerScore` (VO owned) | EXISTING | = `Score` del spec; `CurrentPoints/SecuredPoints/RoundPoints/PotentialPoints/TotalPoints` |
| `ExitedAt` | `DateTimeOffset?` | EXISTING | Momento de retiro/eliminación |
| `CurrentRoundNumber` | `int` | **NEW** | = `CurrentRound` del spec. 0 = sin ronda iniciada. Avanza en `Game.StartRound()` solo si `IsActive`; se congela en `MarkWithdrawn()`/`MarkEliminated()`. Nunca decrece. |

Comportamiento (todo `internal`, solo vía agregado):

- `AdvanceToRound(int roundNumber)` — NEW: `CurrentRoundNumber = roundNumber` (solo jugadores activos, invocado por `Game.StartRound`).
- `MarkWithdrawn()` / `MarkEliminated()` — EXTEND: además de cambiar `ParticipationStatus` y `ExitedAt`, dejan de avanzar `CurrentRoundNumber` (congelación — el avance solo aplica a `Active`).
- `UpdateScore(...)`, `MarkWinner()` — EXISTING.

Restricciones de persistencia (EXTEND `GamePlayerTypeConfiguration`):

- Columna `CurrentRoundNumber` (int, requerida, default 0).
- Índice único `(GameId, UserId)` — EXISTING (FR-013: una participación por usuario/juego).

### PlayerParticipationStatus (Enumeration) — EXISTING

= `Status` del spec (FR-002). No se crea un enum nuevo `PlayerStatus`: el existente cubre exactamente los estados requeridos.

| Valor | Id | Transiciones |
|-------|----|--------------|
| `Active` | 1 | Estado inicial al unirse → `Withdrawn` \| `Eliminated` \| `Winner` |
| `Withdrawn` | 2 | Terminal (no vuelve a `Active`; no acepta respuestas) |
| `Eliminated` | 3 | Terminal (no vuelve a `Active`; no acepta respuestas) |
| `Winner` | 4 | Terminal (marcado en `Game.Finish()` para ganadores) |

### AnswerState — DERIVED (sin entidad nueva)

Estado de respuesta del jugador en la ronda actual (FR-009). Se deriva de la entidad `Answer` existente (una por `(GameId, PlayerId, RoundId)`):

| Condición | AnswerState |
|-----------|-------------|
| Sin `Answer` del jugador en la ronda actual | `NOT_ANSWERED` |
| `Answer` en transición interna | `ANSWERED` (interno transaccional, no expuesto como final) |
| `Answer` evaluado | `EVALUATED` |
| `Answer` expirada (fuera de `TimeLimit`) | `EXPIRED` |

Estados definidos en `AnswerStatus` (Enumeration EXISTING, SPEC-006). Punto de acceso: `Game.GetPlayerAnswerState(playerId)`.

### Answer (Entity&lt;AnswerId&gt;) — EXISTING

Respuesta evaluada por jugador y ronda. Sin cambios de esquema.

- Campos relevantes: `PlayerId` (Guid = UserId), `RoundId`, `QuestionId`, `AnswerOptionId`, `Status` (`AnswerStatus`), `Correct` (bool?), `Points`, `ElapsedTime`, `RowVersion`.
- Índice único `(GameId, PlayerId, RoundId)` — frontera de idempotencia (FR-007, decisión R5).
- Inmutabilidad tras `EVALUATED`/`EXPIRED` (`AnswerImmutabilityRule`).

### PointTransaction (Entity&lt;PointTransactionId&gt;) — EXISTING

Ledger append-only (SPEC-007). Sin cambios. Base de `Points` del leaderboard y del cálculo de consecución más temprana para el desempate (`ResultingBalance` + `CreatedAt`). Índice único `(GameId, AnswerId)` garantiza atomicidad respuesta↔transacción (FR-008).

### LeaderboardEntry (read-model DERIVED) — NEW (solo respuesta de consulta)

No es entidad persistida; se construye en `GetLeaderboardHandler` desde el agregado.

| Campo | Tipo | Fuente |
|-------|------|--------|
| `Rank` | `int` | Posición 1-based tras ordenar (ver reglas de orden) |
| `Player` | `Guid` + `string?` | `GamePlayer.UserId` + `DisplayName` |
| `Points` | `int` | `PlayerScore.CurrentPoints` (consistente con ledger) |
| `CorrectAnswers` | `int` | Conteo de `Answer` del jugador con `Correct == true` |
| `CurrentLevel` | `int?` | `GameRound.Difficulty` de la ronda `CurrentRoundNumber` del jugador; null si `CurrentRoundNumber == 0` |
| `Status` | `string` | `ParticipationStatus.Name` |

Reglas de orden (FR-011, decisión R6) — deterministas y estables:

1. `Points` descendente.
2. Empate → `CorrectAnswers` descendente.
3. Empate → consecución más temprana del saldo actual: menor `CreatedAt` de la `PointTransaction` cuyo `ResultingBalance` estableció el `CurrentPoints` actual (sin transacciones → `JoinedAt`).
4. Empate → `JoinedAt` ascendente (estabilidad final).

Reglas de visibilidad (FR-004, FR-012):

- Visible para todos los participantes del juego y roles organizadores.
- Solo contiene datos evaluados (el ledger solo registra transacciones con `Answer.Status == EVALUATED`); durante una ronda en curso muestra las respuestas ya evaluadas, nunca datos parciales (decisión R8).
- Estable e inmutable tras `FINISHED`.

### PlayerState (read-model DERIVED) — NEW (solo respuesta de consulta)

Respuesta del nuevo slice `GetPlayerState` (FR-015): el estado autoritativo completo del jugador.

| Campo | Tipo | Fuente |
|-------|------|--------|
| `GameId` | `Guid` | Ruta |
| `PlayerId` | `Guid` | `GamePlayer.UserId` |
| `DisplayName` | `string?` | `GamePlayer.DisplayName` |
| `Status` | `string` | `ParticipationStatus.Name` |
| `CurrentPoints` / `SecuredPoints` / `RoundPoints` / `PotentialPoints` / `TotalPoints` | `int` | `PlayerScore` |
| `CurrentRound` | `int` | `GamePlayer.CurrentRoundNumber` |
| `AnswerState` | `string` | `Game.GetPlayerAnswerState(playerId)` (`NOT_ANSWERED`/`EVALUATED`/`EXPIRED`) |
| `CorrectAnswers` / `IncorrectAnswers` | `int` | Conteo de `Answer` con `Correct == true` / `false` |
| `ExitedAt` | `DateTimeOffset?` | `GamePlayer.ExitedAt` |

Visibilidad: solo el propio jugador (`sub == playerId`) u organizadores (FR-004).

## Relaciones

```text
Game (1) ──< (N) GamePlayer          # composición; único (GameId, UserId)
Game (1) ──< (N) GameRound           # composición; único (GameId, RoundNumber)
Game (1) ──< (N) Answer              # composición; único (GameId, PlayerId, RoundId)
Game (1) ──< (N) PointTransaction    # composición; único (GameId, AnswerId); append-only
GamePlayer (1) ──< (N) Answer        # vía (GameId, UserId=PlayerId)
GamePlayer (1) ──< (N) PointTransaction
GameRound (1) ──< (N) Answer
GamePlayer.CurrentRoundNumber ──> GameRound.RoundNumber   # referencia lógica (nivel actual)
```

## Transiciones de estado

### Participación del jugador (`ParticipationStatus`)

```text
JoinGame (WAITING_FOR_PLAYERS)
        │
        ▼
     ACTIVE ──────────► WITHDRAWN   (WithdrawPlayer — SPEC-008; CurrentRoundNumber congelado)
        │
        ├──────────────► ELIMINATED  (política de pérdida — SPEC-008; CurrentRoundNumber congelado)
        │
        └──────────────► WINNER      (Game.Finish() si es ganador)

Terminales: WITHDRAWN / ELIMINATED / WINNER — sin respuestas ni más transiciones (FR-002)
```

### Respuesta del jugador en la ronda (`AnswerState` derivado)

```text
NOT_ANSWERED ──SubmitAnswer válido──► ANSWERED ──EvaluateAnswer──► EVALUATED
      │
      └──────────TimeLimit expirado──────────────────────────────► EXPIRED
```

### Avance de `CurrentRoundNumber`

```text
0 (sin ronda) ──StartRound──► N (solo jugadores ACTIVE)
N ──StartRound──► N+1 (solo jugadores ACTIVE)
N ──Withdraw/Eliminate──► congelado en N (no avanza más)
```

## Invariantes multiplayer (resumen)

1. Un usuario tiene exactamente una participación por juego — índice único `(GameId, UserId)` + `PlayerAlreadyJoined`.
2. Un jugador tiene exactamente una respuesta por ronda — índice único `(GameId, PlayerId, RoundId)` + `ValidateIdempotencyRule`.
3. Toda mutación de estado de jugador pasa por comportamiento del agregado `Game` protegido por `RowVersion` — sin escrituras directas.
4. Ningún jugador muta el estado de otro — identidad `sub` validada en Application (`PlayerIdentityMismatch` → 403).
5. Todo punto proviene de una `PointTransaction` append-only ligada a un `Answer` evaluado — atomicidad respuesta↔puntos.
6. `CurrentRoundNumber` nunca decrece y se congela al terminar la participación.
7. El leaderboard contiene solo datos evaluados y su orden es determinista.
