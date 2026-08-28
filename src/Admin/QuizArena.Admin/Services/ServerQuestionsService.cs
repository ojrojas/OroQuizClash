using QuizArena.Admin.Client.Services;

namespace QuizArena.Admin.Services;

public sealed class ServerQuestionsService(HttpClient httpClient)
    : QuestionsServiceCore(httpClient, "api"), IQuestionsService;
