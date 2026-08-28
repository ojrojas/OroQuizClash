# Contract: Service Interfaces (compartidas cliente/servidor)

**Ubicación**: `src/Admin/QuizArena.Admin.Client/Services/` (+ `Models/`). El proyecto server referencia el Client y registra ambas implementaciones (patrón sample: `IWeatherForecaster` ↔ `ClientWeatherForecaster`/`ServerWeatherForecaster`).

**Registro DI**:
- Cliente WASM: `AddHttpClient<I*Service, Client*Service>(BaseAddress = HostEnvironment.BaseAddress)` → rutas `/bff/*`.
- Servidor: `AddHttpClient<I*Service, Server*Service>(BaseAddress = "http://oroclash-api")` + `AddHttpContextAccessor()`; cada request adjunta `Bearer {GetTokenAsync("access_token")}`.

**Convenciones**: DTOs de `data-model.md`; listas retornan `PagedResult<T>`; fallos de negocio lanzan `ApiErrorException(ApiErrorView)`; cancelación vía `CancellationToken` opcional.

```csharp
public interface IDashboardService
{
    Task<DashboardKpis> GetKpisAsync(CancellationToken ct = default);
}

public interface IGamesAdminService
{
    Task<PagedResult<GameSummary>> GetGamesAsync(GameFilter filter, CancellationToken ct = default);
    Task<GameDetail> GetGameAsync(Guid gameId, CancellationToken ct = default);
    Task<GameDetail> CreateGameAsync(GameConfigurationForm form, CancellationToken ct = default);
    Task<GameDetail> UpdateGameAsync(Guid gameId, GameConfigurationForm form, CancellationToken ct = default);
    Task StartGameAsync(Guid gameId, CancellationToken ct = default);
    Task CancelGameAsync(Guid gameId, CancellationToken ct = default);
    Task FinishGameAsync(Guid gameId, CancellationToken ct = default);
    Task ForceFinishGameAsync(Guid gameId, string reason, CancellationToken ct = default);
    Task OpenLobbyAsync(Guid gameId, CancellationToken ct = default);
    Task<LeaderboardEntry[]> GetLeaderboardAsync(Guid gameId, CancellationToken ct = default);
}
// GameFilter { Status?: GameStatusView, CategoryId?: Guid, Page, PageSize, Search? }

public interface ICategoriesService
{
    Task<PagedResult<CategorySummary>> GetCategoriesAsync(CategoryFilter filter, CancellationToken ct = default);
    Task<CategorySummary> GetCategoryAsync(Guid id, CancellationToken ct = default);
    Task<CategorySummary> CreateCategoryAsync(CategoryForm form, CancellationToken ct = default);
    Task<CategorySummary> UpdateCategoryAsync(Guid id, CategoryForm form, CancellationToken ct = default);
    Task PublishCategoryAsync(Guid id, CancellationToken ct = default);   // si gate falla → ApiErrorException con detalle
    Task ActivateCategoryAsync(Guid id, CancellationToken ct = default);
    Task DeactivateCategoryAsync(Guid id, CancellationToken ct = default);
    Task ArchiveCategoryAsync(Guid id, CancellationToken ct = default);
}

public interface IQuestionsService
{
    Task<PagedResult<QuestionSummary>> GetQuestionsAsync(QuestionFilter filter, CancellationToken ct = default);
    Task<QuestionSummary> GetQuestionAsync(Guid id, CancellationToken ct = default);
    Task<QuestionSummary> CreateQuestionAsync(QuestionForm form, CancellationToken ct = default);
    Task<QuestionSummary> UpdateQuestionAsync(Guid id, QuestionForm form, CancellationToken ct = default);
    Task PublishQuestionAsync(Guid id, CancellationToken ct = default);
    Task ActivateQuestionAsync(Guid id, CancellationToken ct = default);
    Task DeactivateQuestionAsync(Guid id, CancellationToken ct = default);
    Task ArchiveQuestionAsync(Guid id, CancellationToken ct = default);
}

public interface IPlayersService
{
    Task<PlayerStatusView> GetPlayerStatusAsync(Guid gameId, Guid playerId, CancellationToken ct = default);
    Task<ConsolationHistoryEntry[]> GetConsolationHistoryAsync(Guid playerId, CancellationToken ct = default);
    Task<PagedResult<PlayerStatusView>> GetGamePlayersAsync(Guid gameId, int page, int pageSize, CancellationToken ct = default);
}

public interface IRewardsService
{
    Task<PagedResult<RewardSummary>> GetRewardsAsync(int page, int pageSize, CancellationToken ct = default);
    Task<RewardSummary> CreateRewardAsync(RewardForm form, CancellationToken ct = default);
    Task<RewardSummary> UpdateRewardAsync(Guid rewardId, RewardForm form, CancellationToken ct = default);
    Task ActivateRewardAsync(Guid rewardId, CancellationToken ct = default);
    Task DeactivateRewardAsync(Guid rewardId, CancellationToken ct = default);
}

public interface IRedemptionsService
{
    Task<PagedResult<RedemptionSummary>> GetRedemptionsAsync(RedemptionFilter filter, CancellationToken ct = default);
    Task ApproveAsync(Guid redemptionId, CancellationToken ct = default);
    Task RejectAsync(Guid redemptionId, CancellationToken ct = default);
    Task CancelAsync(Guid redemptionId, CancellationToken ct = default);
    Task DeliverAsync(Guid redemptionId, CancellationToken ct = default);
}
// RedemptionFilter { Status?: RedemptionStatusView, Page, PageSize }

public interface IReportsService
{
    Task<ReportResult> GetGameReportAsync(Guid gameId, CancellationToken ct = default);
    Task<ReportResult> GetCategoryReportAsync(Guid categoryId, CancellationToken ct = default);
    Task<ReportResult> GetQuestionReportAsync(Guid questionId, CancellationToken ct = default);
    Task<ReportResult> GetPlayerReportAsync(Guid playerId, CancellationToken ct = default);
    Task<ReportResult> GetRewardsReportAsync(DateRange? period, CancellationToken ct = default);
    Task<ReportResult> GetLeaderboardReportAsync(DateRange? period, CancellationToken ct = default);
}

public interface IAuditService
{
    Task<PagedResult<AuditEntry>> GetAuditAsync(AuditFilter filter, CancellationToken ct = default);
    Task<AuditEntry> GetAuditDetailAsync(Guid id, CancellationToken ct = default);
}
// AuditFilter { Actor?: string, Action?: string, From?: DateTimeOffset, To?: DateTimeOffset, Page, PageSize }

public interface ILiveGamesService
{
    Task<PagedResult<LiveGameSummary>> GetLiveGamesAsync(CancellationToken ct = default);
    Task<LiveGameSubscription> SubscribeAsync(Guid gameId, CancellationToken ct = default);
    Task StopGameAsync(Guid gameId, string reason, CancellationToken ct = default); // force-finish con confirmación en UI
}
// LiveGameSubscription: IAsyncEnumerable/eventos GameStarted|PlayerJoined|RoundStarted|RoundCompleted|GameFinished|LeaderboardUpdated
// + ConnectionState (Connected|Reconnecting|Disconnected); Dispose() libera la conexión SignalR.
```

**Contrato de implementación dual**: para toda interfaz, `Client*Service` y `Server*Service` MUST tener comportamiento observable idéntico (mismos DTOs, mismos errores); la única diferencia es el transporte (BFF cookie vs llamada directa con Bearer).
