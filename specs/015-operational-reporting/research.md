# Research: Operational Reporting (SPEC-015)

**Date**: 2026-08-28 | **Plan**: [plan.md](plan.md) | **Spec**: [spec.md](spec.md)

Phase 0 — resolución de decisiones técnicas. No quedó ningún NEEDS CLARIFICATION en el Technical Context; las decisiones siguientes resuelven los puntos de diseño identificados tras inspeccionar el código existente (`LeaderboardBuilder`, `Game`/`Answer`/`PointTransaction`, `RewardRedemption`, `AuditEntry`, `Specification`).

## R1 — Fuente de `PointsEarned` y `Winner` — reutilizar ledger, no recalcular

- **Decision**: `PointsEarned` y `Winner` se derivan exclusivamente de `PointTransaction` ledger (SPEC-007) y `LeaderboardBuilder` existente (SPEC-011). `PointsEarned` = suma de `PointTransaction` del jugador donde `Type` ∈ `ANSWER_CORRECT`/`ROUND_BONUS`/`LEVEL_BONUS`/`GAME_BONUS` dentro del periodo; `Winner` = `LeaderboardBuilder.Build(game)` rank 1 cuando `Game.Status == FINISHED`, si no `null`.
- **Rationale**: FR-011 exige reutilizar ledger, no recalcular desde campo cliente. `LeaderboardBuilder` ya implementa desempates deterministas y es la fuente auditada.
- **Alternatives considered**:
  - Campo `Score` en `GamePlayer` como fuente: rechazado — es snapshot, no trazable por periodo.
  - Recalcular desde `Answer.Correct` + `PointsPerRound`: rechazado — duplica lógica de `ScoringSystem` y diverge del ledger.

## R2 — `Accuracy` y `GamesPlayed/Won/Lost/Withdrawn` — definición precisa

- **Decision**: `Accuracy` = `CorrectAnswers` / `QuestionsAnswered` × 100, con `0` si `QuestionsAnswered==0` (evita división por cero). `GamesPlayed` = count de `Game` donde `GamePlayer` pertenece y `Game.Status == FINISHED`; `GamesWon` = donde `Winner.PlayerId == PlayerId`; `GamesLost` = `GamesPlayed` − `GamesWon` − `GamesWithdrawn`; `GamesWithdrawn` = donde `GamePlayer.Status == WITHDRAWN`.
- **Rationale**: FR-002 y SC-002 exigen definiciones exactas para que los contadores sean verificables vs ledger. `GamesPlayed` no cuenta juegos en curso ni retirados a mitad (edge case).
- **Alternatives considered**:
  - Contar todos los juegos donde participó (incluyendo `IN_PROGRESS`): rechazado — infla `GamesPlayed` y rompe SC-002.
  - `GamesLost` = todos los no ganados: rechazado — no distingue retirada.

## R3 — `AverageResponseTime` — solo `Evaluated`

- **Decision**: `AverageResponseTime` = `AVG(Answer.ElapsedTime)` donde `Answer.Status == EVALUATED` y `Answer.QuestionId == Question.Id` y `Answer` pertenece a un `GameRound` con `QuestionId` dentro del filtro de periodo/categoría. `TimesPresented` = `COUNT(GameRound where QuestionId == Question.Id)` dentro del filtro; `CorrectAnswers`/`IncorrectAnswers` usan solo `Evaluated`.
- **Rationale**: FR-010 y edge case exigen que presentaciones sin evaluación (timeout) no contaminen promedios. `ElapsedTime` solo existe en evaluadas.
- **Alternatives considered**:
  - Promediar todas las `Submitted`: rechazado — incluye respuestas sin `ElapsedTime` o con tiempo cliente no confiable.
  - Usar `Answer.CreatedAt` − `Round.StartedAt`: rechazado — duplica cálculo ya hecho en `ElapsedTime`.

## R4 — `TimesPresented` vs `Answers` — fuente distinta

- **Decision**: `TimesPresented` cuenta `GameRound` (presentaciones), no `Answer` (respuestas). Una pregunta puede ser presentada y no respondida (withdrawn/timeout) y aún debe contar.
- **Rationale**: FR-003 y edge case lo explicitan; usar `GameRound` es la única fuente que registra presentaciones aunque no haya `Answer`.
- **Alternatives considered**:
  - Contar `Answer` totales: rechazado — subestima presentaciones sin respuesta.

## R5 — Filtros `Global`/`Game`/`Category`/`Period` — composición con `Specification`

- **Decision**: Cada `IQueryHandler` de reporte construye `Specification<T>` compuesta con `Where` dinámico: si `gameId` != null → `Where(g => g.Id == gameId)` o `Where(p => p.GameId == gameId)`; si `categoryId` != null → `Where(q => q.CategoryId == categoryId)` o `Where(g => g.CategoryId == categoryId)` vía join; si `from`/`to` != null → `Where(e => e.Timestamp >= from && e.Timestamp <= to)` sobre `PointTransaction.CreatedAt`/`Answer.CreatedAt`/`RewardRedemption.RequestedAt`. `Global` = sin `Where` adicional. Combinaciones son intersección (`AND`). Validación `from` ≤ `to` en `Validator`.
- **Rationale**: FR-007 exige filtros combinables y no filtrar cuando es `null`; `Specification` ya es el patrón del proyecto y permite `AsNoTracking` + paginación sin SQL crudo.
- **Alternatives considered**:
  - Query con `if`/`IQueryable` manual sin `Specification`: rechazado — inconsistente con el resto y duplica lógica.
  - View SQL materializada: rechazado — sobrediseño para volumen moderado y rompería single-node simple.

## R6 — `Leaderboard` extendido sin duplicar lógica

- **Decision**: Extender `GetLeaderboardQuery` existente (SPEC-011) con parámetros opcionales `CategoryId`/`From`/`To` además de `GameId`. El handler reutiliza `LeaderboardBuilder.Build(game)` y luego filtra `PointTransaction` por periodo/categoría antes de construir el ranking, o bien reconstruye el ranking filtrando el scope. Sin duplicar `LeaderboardEntry` ni lógica de desempate.
- **Rationale**: FR-006 exige no duplicar lógica de ranking; `LeaderboardBuilder` ya es la fuente determinista y auditada.
- **Alternatives considered**:
  - Nuevo `GetLeaderboardExtendedQuery` duplicando lógica: rechazado — duplica desempate y diverge.
  - `Leaderboard` separado por `Category`/`Period` con tabla agregada: rechazado — eventually consistent y no necesario para lectura <200 ms.

## R7 — No-mutación y 0 side-effects — verificación

- **Decision**: Todos los handlers de reporte usan `IRepository` con `ApplyAsNoTracking()` y nunca llaman `AddAsync`/`Update`/`SaveChanges`. Verificación en tests: contar `PointTransaction`/`AuditEntries`/`RewardRedemptions` antes y después de `IQuery` (SC-005) y reconstrucción idempotente en dos ejecuciones. `IQuery` nunca publica `DomainEvent`.
- **Rationale**: FR-008 es transversal y crítica; `AsNoTracking` + ausencia de `SaveChanges` es la garantía más simple.
- **Alternatives considered**:
  - Flag `IsReadOnly` en `DbContext`: rechazado — no existe en `AppDbContextBase`.
  - Transacción de solo lectura a nivel BD: rechazado — sobrediseño.

