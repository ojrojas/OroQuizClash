using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

/// <summary>
/// InteractiveServer dashboard: composes the Server*Service implementations.
/// </summary>
public sealed class ServerDashboardService(
    IGamesAdminService games,
    IQuestionsService questions,
    IRedemptionsService redemptions,
    IReportsService reports,
    ICategoriesService categories,
    IRewardsService rewards)
    : DashboardServiceCore(games, questions, redemptions, reports, categories, rewards), IDashboardService;
