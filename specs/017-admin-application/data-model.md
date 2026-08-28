# Data Model: QuizArena Administration Application

**Branch**: `017-admin-application` | **Date**: 2026-08-28 | **Plan**: [plan.md](plan.md)

Modelo de datos del **frontend** (proyecciones/DTOs). La aplicación no persiste nada: estas estructuras viven en `QuizArena.Admin.Client/Models/` y reflejan los contratos REST de `QuizArena.Api` (SPEC-001..015). La autoridad de negocio permanece en el backend (Constitución V).

## 1. DTOs por sección

### Games

- **GameSummary**: `Id: Guid`, `Name: string`, `CategoryName: string`, `Status: GameStatusView`, `MinPlayers/MaxPlayers: int`, `CurrentPlayers: int`, `CurrentRound: int?`, `TotalRounds: int`, `CreatedAt: DateTimeOffset`
- **GameDetail**: GameSummary + `Description`, `Difficulty: int`, `QuestionsPerRound: int`, `TimeLimitSeconds: int`, `EntryFee: decimal?`, `RewardPool: decimal?`, `StartedAt/FinishedAt: DateTimeOffset?`, `Rounds: RoundSummary[]`, `Leaderboard: LeaderboardEntry[]`
- **GameConfigurationForm** (FR-012): `Name` (3–100), `Description` (≤500), `CategoryId: Guid` (requerido), `Difficulty` (1–5), `Rounds` (1–10), `QuestionsPerRound` (1–20), `TimeLimitSeconds` (≥5), `MinPlayers` (≥1), `MaxPlayers` (≥MinPlayers), `EntryFee?` (≥0), `RewardPool?` (≥0)
  - **Validación** (inline, espejo del dominio): todos los rangos anteriores; errores de negocio del API (p. ej., `CategoryNotReady`) se muestran como mensaje de formulario.
- **RoundSummary**: `RoundNumber: int`, `Status: string`, `CompletedAt: DateTimeOffset?`
- **LeaderboardEntry**: `Rank: int`, `PlayerName: string`, `Score: int`, `IsCurrentOperator: bool` (solo agregados públicos — privacidad SPEC-016 §11)
- **GameStatusView** (enumeración de presentación): `Configuring | Lobby | Active | Finished | Cancelled`
  - **Transiciones visibles**: `Configuring → Lobby (open-lobby/ready) → Active (start) → Finished (finish/force-finish)`; `Configuring|Lobby → Cancelled (cancel)`. Edición permitida solo en `Configuring` (FR-013).

### Categories

- **CategorySummary**: `Id`, `Name`, `KnowledgeArea`, `AcademicLevel`, `AgeMin/AgeMax: int`, `Difficulty: int`, `Tags: string[]`, `Status: CategoryStatusView`, `ValidQuestionCount: int`, `UpdatedAt`
- **CategoryForm**: `Name` (3–100), `Description?`, `KnowledgeArea` (2–100), `AcademicLevel` (2–100), `AgeMin/AgeMax` (0–120, min≤max), `Difficulty` (1–5), `Tags` (≤10, 2–30 chars c/u)
- **CategoryStatusView**: `Draft | Active | Inactive | Archived` — transiciones espejo de SPEC-002 (`Publish` requiere gate ≥5 válidas; mensaje con faltantes si no cumple).

### Questions

- **QuestionSummary**: `Id`, `Text` (truncado 60ch), `CategoryName`, `Difficulty`, `Status: QuestionStatusView`, `InUseByLiveGame: bool`, `UpdatedAt`
- **QuestionForm**: `Text` (≥10), `CategoryId`, `Difficulty` (1–5), `Options: OptionForm[4]` (exactamente 4), `OptionForm { Text (1–200), IsCorrect: bool }` con exactamente 1 `IsCorrect`, `Explanation?`
- **QuestionStatusView**: `Draft | Active | Inactive | Archived`; `InUseByLiveGame=true` → solo-lectura (FR-019).

### Players

- **PlayerStatusView**: `PlayerId: Guid`, `DisplayName: string`, `GameId: Guid`, `State: string` (JOINED/PLAYING/WITHDRAWN/ELIMINATED/FINISHED), `CurrentRound: int?`, `SecuredPoints: int` (agregado público)
- **ConsolationHistoryEntry**: `GameId`, `AwardedAt`, `Points: int`, `Reason: string`
- Nota: no existe listado global de identidades (la identidad pertenece a OroIdentityServer); la sección Players se construye por juego (Assumptions del spec).

### Rewards / Redemptions

- **RewardSummary**: `Id`, `Name`, `Description`, `PointCost: int`, `Status: RewardStatusView`, `Stock: int?`
- **RewardForm**: `Name`, `Description`, `PointCost` (>0), `Stock?` (≥0)
- **RewardStatusView**: `Active | Inactive`
- **RedemptionSummary**: `Id`, `PlayerName`, `RewardName`, `PointCost`, `Status: RedemptionStatusView`, `RequestedAt`, `DecidedAt?`, `DecidedBy?`
- **RedemptionStatusView**: `Pending | Approved | Rejected | Delivered | Cancelled`
  - **Transiciones permitidas al gestor**: `Pending → Approved | Rejected | Cancelled`; `Approved → Delivered`. Terminales: `Rejected`, `Delivered`, `Cancelled` (sin re-proceso, FR-025).

### Reports / Audit / Dashboard

- **ReportResult**: `Title: string`, `Period: DateRange?`, `Columns: string[]`, `Rows: IReadOnlyList<object[]>` (forma tabular genérica para los 6 tipos de reporte)
- **AuditEntry**: `Id`, `Timestamp`, `ActorName`, `Action: string`, `EntityType/EntityId`, `Summary`, `DetailJson?` (expandible, solo-lectura)
- **DashboardKpis**: `ActiveGames: int`, `PlayersOnline: int`, `QuestionBankSize: int`, `PendingRedemptions: int`, `RewardsPaidPeriod: decimal`, `GamesPeriod: int`
- **LiveGameSummary**: `GameId`, `Name`, `CategoryName`, `ActivePlayers/TotalPlayers: int`, `Round: int`, `TotalRounds: int`, `Status`, `StartedAt`, `ConnectionState: LiveConnectionView`

## 2. Estado de autenticación (proyección)

- **AdminUserState**: `IsAuthenticated: bool`, `DisplayName: string`, `Roles: string[]` (ADMIN/GAME_MANAGER/REWARD_MANAGER), `MustChangePassword: bool`
  - Fuente: `AuthenticationState` del servidor (claims `name`, `roles`, `must_change_password`) serializado al cliente (`SerializeAllClaims = true`).
  - `MustChangePassword=true` → bloqueo de navegación y redirección al flujo del proveedor (FR-004).

## 3. Estado de conexión en vivo

- **LiveConnectionView**: `Connected | Reconnecting | Disconnected`
  - `Reconnecting/Disconnected` → banner + inputs de detener-juego deshabilitados; al reconectar, re-consulta REST completa (Server Truth, R3/R5).

## 4. Modelo de errores

- **ApiErrorView**: `Code: string` (código de error de negocio del API, p. ej., `CategoryNotReady`, `InvalidGameState`), `Title: string`, `Detail: string?`, `FieldErrors: Dictionary<string,string[]>`
  - Mapeo desde `ProblemDetails` (RFC 7807) que retorna el API; nunca se muestran stack traces ni detalles internos (FR-031).

## 5. Relaciones

```text
DashboardKpis ── deriva de ──> ReportResult (varios)
GameDetail ── contiene ──> RoundSummary[], LeaderboardEntry[]
GameConfigurationForm ── referencia ──> CategorySummary (CategoryId)
QuestionForm ── referencia ──> CategorySummary (CategoryId)
RedemptionSummary ── referencia ──> RewardSummary, PlayerStatusView (por nombre/id)
LiveGameSummary ── actualiza ──> GameSummary (mismo GameId, fuente REST tras eventos)
AdminUserState ── gobierna ──> NavMenu (secciones visibles) y políticas de ruta
```

## 6. Reglas transversales

- Todos los DTOs son **inmutables** (records) y serializables JSON (camelCase, `System.Text.Json`).
- Ningún DTO contiene lógica de negocio: solo proyección/validación de formato de formulario (las reglas de negocio las impone el API).
- Paginación: todas las listas usan `PagedResult<T> { Items: T[], TotalCount: int, Page: int, PageSize: int }`.
- Fechas en `DateTimeOffset` UTC; la UI localiza la presentación.
