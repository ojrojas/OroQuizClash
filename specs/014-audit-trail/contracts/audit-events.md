# Contract: Audit Events — SPEC-014

**Date**: 2026-08-28 | **Plan**: [../plan.md](../plan.md) | **Spec**: [../spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

Catálogo cerrado de 16 `Action` auditables (FR-002). Cada `Action` se genera exactamente una vez por intento (éxito o fracaso) vía `AuditBehavior` centralizado (FR-010), sin que la auditoría condicione la lógica de negocio (FR-008).

## Catálogo

| Action | Actor típico | Resource | ResourceId | GameId | PlayerId | Cuándo se genera |
|--------|--------------|----------|------------|--------|----------|------------------|
| `GameCreated` | `ADMIN`/`GAME_MANAGER` (`sub`) | `Game` | `GameId` | `GameId` | — | `CreateGame` comando exitoso |
| `GameConfigured` | `ADMIN`/`GAME_MANAGER` | `Game` | `GameId` | `GameId` | — | `UpdateGame`/`ConfigureGame` |
| `GameStarted` | `ADMIN`/`GAME_MANAGER` | `Game` | `GameId` | `GameId` | — | `StartGame` |
| `PlayerJoined` | `PLAYER` (`sub`) | `Player` | `PlayerId` | `GameId` | `PlayerId` | `JoinGame` |
| `RoundStarted` | `ADMIN`/`GAME_MANAGER` | `Round` | `RoundId` | `GameId` | — | `StartRound` |
| `QuestionPresented` | `System` | `Question` | `QuestionId` | `GameId` | — | `StartRound` (misma tx que `RoundStarted`) |
| `AnswerSubmitted` | `PLAYER` (`sub`) | `Answer` | `AnswerId` (o `null` si validación falló) | `GameId` | `PlayerId` | `SubmitAnswer` recepción (incluso si `Denied`/`ValidationFailed`) |
| `AnswerEvaluated` | `System` | `Answer` | `AnswerId` | `GameId` | `PlayerId` | Evaluación de corrección |
| `PointsAwarded` | `System` | `Player` | `PlayerId` | `GameId` | `PlayerId` | `ScoreUpdated` delta >0 |
| `PointsRemoved` | `System` | `Player` | `PlayerId` | `GameId` | `PlayerId` | `ScoreUpdated` delta <0 / penalización |
| `PlayerWithdrawn` | `PLAYER` (`sub`) | `Player` | `PlayerId` | `GameId` | `PlayerId` | `WithdrawPlayer` |
| `PlayerEliminated` | `System` | `Player` | `PlayerId` | `GameId` | `PlayerId` | `EliminatePlayer` / regla |
| `GameFinished` | `System`/`ADMIN` | `Game` | `GameId` | `GameId` | — | `FinishGame` |
| `RewardRedeemed` | `PLAYER` (`sub`) | `Reward` | `RewardId` | `GameId` | `PlayerId` | `RedeemReward` |
| `ConsolationGranted` | `System` | `Consolation` | `ConsolationId` | `GameId` | `PlayerId` | `GrantConsolation` |
| `AdministrativeAdjustment` | `ADMIN` (`sub`) | `Player`/`Game` | `TargetId` | `GameId` | `PlayerId` (si aplica) | `AdjustPoints`/`Admin` ops |

**Notas:**

- `Action` es `AuditAction.Name` (string) del `Enumeration` `AuditAction`.
- `Result` del `AuditRecord` refleja el resultado del intento: `Succeeded`/`Failed`/`Denied`/`RateLimited`/`ReplayDetected` (no confundir con `PointsAwarded` que siempre es `Succeeded` si se otorgaron puntos, incluso si la respuesta fue incorrecta con `PointsRemoved`).
- `Data` (JSON) contiene detalles mínimos sanitizados (ej. `{"delta":10,"balance":120}` para `PointsAwarded`, `{"questionId":"...","answerOptionId":"..."}` para `AnswerSubmitted`, `{"reason":"timeout"}` para `PlayerEliminated`).

## Mapeo a código

- **Producer**: `AuditBehavior<TRequest,TResponse>` en `Application/Behaviors/AuditBehavior.cs` (EXTEND de SPEC-013) — mapea `TRequest` (`CreateGameCommand`, `JoinGameCommand`, `StartRoundCommand`, `SubmitAnswerCommand`, `RedeemRewardCommand`, etc.) a `AuditAction` vía `switch`/`Dictionary<Type, AuditAction>`.
- **Consumer**: `GetAuditEntries` (`Features/Audit/GetAuditEntries.cs`) filtra por `Action` (`Where(e => e.Action == action)`).
- **Store**: `AuditEntry` (`Domain/Audit/AuditEntry.cs`) + `AuditEntryTypeConfiguration` (índices `GameId`, `PlayerId`, `Action`, `CorrelationId`, `Timestamp`).

## Reglas

- **FR-004/005**: solo `AddAsync`, nunca `Update`/`Delete`; intento de `PUT`/`DELETE` sobre audit → 405.
- **FR-008**: ningún handler de dominio (`Games`, `Rewards`) inyecta `IRepository<AuditEntry>`; la auditoría es observabilidad, no dependency.
- **FR-009**: incluso intentos fallidos (`ValidationFailed`, `Denied`) generan `AuditRecord` con `Result` correspondiente.
- **FR-010**: generación centralizada en `AuditBehavior`, no dispersa por feature.

