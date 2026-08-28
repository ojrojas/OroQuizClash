using QuizArena.Admin.Client.Models;

namespace QuizArena.Admin.Client.Models.Categories;

public sealed record Category(
    Guid CategoryId,
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    string TargetAudience,
    CategoryStateView Status,
    CategoryMetadata Metadata,
    ProgressionRule Progression,
    int ValidQuestionCount,
    string RowVersion);

public sealed record CategorySummary(
    Guid Id,
    string Name,
    string KnowledgeArea,
    CategoryStateView Status,
    int ValidQuestionCount,
    string RowVersion);

public sealed record CategoryDetail(
    Guid Id,
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    string TargetAudience,
    CategoryStateView Status,
    CategoryMetadata Metadata,
    ProgressionRule Progression,
    int ValidQuestionCount,
    string RowVersion,
    IReadOnlyList<CategoryStateTransition> History);

public sealed record CategoryStateTransition(
    CategoryStateView From,
    CategoryStateView To,
    DateTimeOffset Timestamp,
    string ActorId,
    string? Reason);

public sealed record CategoryAuditEntry(
    Guid CategoryId,
    string ActorId,
    DateTimeOffset Timestamp,
    CategoryStateView FromState,
    CategoryStateView ToState,
    IReadOnlyDictionary<string,string> ChangedFields,
    string CorrelationId,
    string Result);

public sealed record CreateCategoryRequest(
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    string TargetAudience,
    IReadOnlyList<string> Tags,
    string? Color,
    string? Icon,
    ProgressionRule Progression);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description,
    string KnowledgeArea,
    string AcademicLevel,
    int AgeMin,
    int AgeMax,
    int Difficulty,
    string TargetAudience,
    IReadOnlyList<string> Tags,
    string? Color,
    string? Icon,
    ProgressionRule Progression,
    string RowVersion);

public sealed record CategoryFilter(
    CategoryStateView? Status = null,
    string? KnowledgeArea = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);
