namespace QuizArena.Admin.Client.Models.Questions;

public sealed record AnswerOption(
    Guid OptionId,
    string Text,
    bool IsCorrect,
    char Position);

public sealed record Question(
    Guid QuestionId,
    string Text,
    Guid CategoryId,
    string CategoryName,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int TimePerQuestion,
    string? Explanation,
    QuestionStateView Status,
    IReadOnlyList<AnswerOption> Answers,
    int CorrectAnswerIndex,
    string RowVersion);

public sealed record QuestionSummary(
    Guid Id,
    string Text,
    Guid CategoryId,
    string CategoryName,
    int Difficulty,
    QuestionStateView Status,
    int TimePerQuestion,
    bool InUseByLiveGame,
    string RowVersion);

public sealed record QuestionDetail(
    Guid Id,
    string Text,
    Guid CategoryId,
    string CategoryName,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int TimePerQuestion,
    string? Explanation,
    QuestionStateView Status,
    IReadOnlyList<AnswerOption> Answers,
    bool InUseByLiveGame,
    string RowVersion,
    IReadOnlyList<QuestionStateTransition> History);

public sealed record QuestionStateTransition(
    QuestionStateView From,
    QuestionStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);

public sealed record QuestionAuditEntry(
    Guid QuestionId,
    string ActorId,
    DateTimeOffset Timestamp,
    string Action,
    QuestionStateView FromState,
    QuestionStateView ToState,
    IReadOnlyDictionary<string,string> ChangedFields,
    string CorrelationId,
    string Result);

public sealed record CreateQuestionRequest(
    string Text,
    Guid CategoryId,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    IReadOnlyList<OptionForm> Options,
    string? Explanation,
    int TimePerQuestion);

public sealed record UpdateQuestionRequest(
    string Text,
    Guid CategoryId,
    int Difficulty,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    IReadOnlyList<OptionForm> Options,
    string? Explanation,
    int TimePerQuestion,
    string RowVersion);

public sealed record OptionForm(string Text, bool IsCorrect);

public sealed record QuestionFilter(
    Guid? CategoryId = null,
    int? Difficulty = null,
    QuestionStateView? Status = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public sealed record QuestionStatistics(
    int Total,
    IReadOnlyList<CountByCategory> ByCategory,
    IReadOnlyList<CountByDifficulty> ByDifficulty,
    IReadOnlyList<CountByStatus> ByStatus,
    double AvgTimePerQuestion,
    IReadOnlyList<ValidCountPerCategory> ValidPerCategory);

public sealed record CountByCategory(Guid CategoryId, string CategoryName, int Count);
public sealed record CountByDifficulty(int Difficulty, int Count);
public sealed record CountByStatus(QuestionStateView Status, int Count);
public sealed record ValidCountPerCategory(Guid CategoryId, string CategoryName, int Valid, int Required);

public sealed record SystemConfig(int CategoryMinQuestions);
