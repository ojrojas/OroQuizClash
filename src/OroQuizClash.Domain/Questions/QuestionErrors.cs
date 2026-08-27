using BuildingBlocks.Kernel.Domain.Results;

namespace OroQuizClash.Domain.Questions;

public static class QuestionErrors
{
    public static Error QuestionMustHaveFourOptions => Error.Validation("Question.MustHaveFourOptions", "Question must have exactly 4 answer options (QST-001).");
    public static Error QuestionMustHaveOneCorrectAnswer => Error.Validation("Question.MustHaveOneCorrectAnswer", "Question must have exactly 1 correct answer (QST-002).");
    public static Error QuestionMustBelongToCategory => Error.Validation("Question.MustBelongToCategory", "Question must belong to a category (QST-003).");
    public static Error CategoryNotFound(Guid? categoryId = null) => Error.NotFound("Question.CategoryNotFound", $"Category {(categoryId?.ToString() ?? "null")} not found or is archived.");
    public static Error QuestionMustHaveDifficulty => Error.Validation("Question.MustHaveDifficulty", "Question must have a difficulty (QST-004).");
    public static Error QuestionNotPublishable(string? detail = null) => Error.Validation("Question.NotPublishable", detail ?? "Question is not publishable: requires exactly 4 options, 1 correct, Category and Difficulty (QST-006).");
    public static Error PublishedQuestionMustHaveCorrectAnswer => Error.Validation("Question.PublishedMustHaveCorrectAnswer", "A published question cannot be left without a correct answer (QST-005).");
    public static Error InvalidQuestionState(string detail) => Error.Validation("Question.InvalidState", detail);
    public static Error QuestionNotFound(Guid id) => Error.NotFound("Question.NotFound", $"Question {id} not found.");
    public static Error AnswerOptionNotFound(Guid id) => Error.NotFound("Question.AnswerOptionNotFound", $"AnswerOption {id} not found.");
    public static Error NoAvailableQuestion => Error.NotFound("Question.NoAvailableQuestion", "No available question matches the criteria and exclusions.");
    public static Error InvalidQuestionText(string detail) => Error.Validation("Question.InvalidText", detail);
    public static Error InvalidAgeRange(string detail) => Error.Validation("Question.InvalidAgeRange", detail);
    public static Error InvalidAcademicLevel(string detail) => Error.Validation("Question.InvalidAcademicLevel", detail);
    public static Error InvalidAnswerOptionText(string detail) => Error.Validation("Question.InvalidAnswerOptionText", detail);
    public static Error DuplicateAnswerOption => Error.Validation("Question.DuplicateAnswerOption", "Answer options must have unique text within the same question.");
    public static Error ConcurrencyConflict => Error.Conflict("Question.ConcurrencyConflict", "Question was modified by another request. Please reload and retry.");
}
