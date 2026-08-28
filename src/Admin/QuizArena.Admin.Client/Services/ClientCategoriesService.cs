namespace QuizArena.Admin.Client.Services;

public sealed class ClientCategoriesService(HttpClient httpClient)
    : CategoriesServiceCore(httpClient, "bff"), ICategoriesService;
