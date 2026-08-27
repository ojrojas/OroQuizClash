using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Questions;

public sealed class QuestionStatus(int id, string name) : Enumeration<QuestionStatus>(id, name)
{
    public static readonly QuestionStatus Draft = new(1, "DRAFT");
    public static readonly QuestionStatus Active = new(2, "ACTIVE");
    public static readonly QuestionStatus Published = new(3, "PUBLISHED");
    public static readonly QuestionStatus Inactive = new(4, "INACTIVE");
    public static readonly QuestionStatus Archived = new(5, "ARCHIVED");

    public bool IsTerminal => this == Archived;

    public bool IsDraft => this == Draft;

    public bool IsActive => this == Active;

    public bool IsPublished => this == Published;

    public bool IsInactive => this == Inactive;

    public bool IsArchived => this == Archived;

    /// <summary>Only PUBLISHED is available for game selection (QST-006).</summary>
    public bool IsAvailableForSelection => this == Published;

    /// <summary>Whether Update is allowed: DRAFT/INACTIVE always, PUBLISHED only if keeps 4/1 (checked separately).</summary>
    public bool CanBeUpdated => this == Draft || this == Inactive || this == Published;

    public bool CanBeActivatedFrom => this == Draft || this == Inactive;

    public bool CanBeDeactivatedFrom => this == Active || this == Published;

    public bool CanBePublishedFrom => this == Draft || this == Active || this == Inactive;

    public bool CanBeArchivedFrom => this == Active || this == Published || this == Inactive;

    public static bool IsValidTransition(QuestionStatus from, QuestionStatus to)
    {
        if (from == Draft && (to == Active || to == Published || to == Archived)) return true;
        if (from == Active && (to == Published || to == Inactive || to == Archived)) return true;
        if (from == Published && (to == Inactive || to == Archived)) return true;
        if (from == Inactive && (to == Active || to == Published || to == Archived)) return true;
        return false;
    }
}
