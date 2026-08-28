using QuizArena.Admin.Client.Models.Dashboard;

namespace QuizArena.Admin.Client.Services;

public static class DashboardRouteMap
{
    public static string RouteFor(MetricId id) => id switch
    {
        MetricId.ActiveGames => "/admin/games?status=Active",
        MetricId.ScheduledGames => "/admin/games?status=Scheduled",
        MetricId.FinishedGames => "/admin/games?status=Finished",
        MetricId.ConnectedPlayers => "/admin/players?view=online",
        MetricId.ActivePlayers => "/admin/players?view=active",
        MetricId.AvailableQuestions => "/admin/questions?status=Active",
        MetricId.Categories => "/admin/categories?status=Active",
        MetricId.Rewards => "/admin/rewards?status=Active",
        MetricId.Redemptions => "/admin/rewards?status=Pending",
        MetricId.GeneralStatistics => "/admin/reports?focus=general",
        _ => "/admin/dashboard"
    };

    public static string LabelFor(MetricId id) => id switch
    {
        MetricId.ActiveGames => "Juegos activos",
        MetricId.ScheduledGames => "Juegos programados",
        MetricId.FinishedGames => "Juegos finalizados",
        MetricId.ConnectedPlayers => "Jugadores conectados",
        MetricId.ActivePlayers => "Jugadores activos",
        MetricId.AvailableQuestions => "Preguntas disponibles",
        MetricId.Categories => "Categorías",
        MetricId.Rewards => "Premios",
        MetricId.Redemptions => "Canjes",
        MetricId.GeneralStatistics => "Estadísticas generales",
        _ => id.ToString()
    };
}
