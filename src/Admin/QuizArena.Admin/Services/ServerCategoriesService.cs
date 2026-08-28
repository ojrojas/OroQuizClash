using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

public sealed class ServerCategoriesService(HttpClient httpClient)
    : CategoriesServiceCore(httpClient, "api"), ICategoriesService;
