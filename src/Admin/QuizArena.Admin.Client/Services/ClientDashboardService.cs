namespace QuizArena.Admin.Client.Services;

/// <summary>
/// WASM dashboard: composes the Client*Service implementations (resolved from the
/// WebAssembly DI container) into KPIs.
/// </summary>
public sealed class ClientDashboardService(
    IGamesAdminService games,
    IQuestionsService questions,
    IRedemptionsService redemptions,
    IReportsService reports)
    : DashboardServiceCore(games, questions, redemptions, reports), IDashboardService;
