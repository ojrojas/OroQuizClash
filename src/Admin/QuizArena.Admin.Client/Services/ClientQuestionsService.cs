namespace QuizArena.Admin.Client.Services;

public sealed class ClientQuestionsService(HttpClient httpClient)
    : QuestionsServiceCore(httpClient, "bff"), IQuestionsService;
