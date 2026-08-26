using BuildingBlocks.Kernel.Domain.Enumerations;

namespace OroQuizClash.Domain.Categories;

public sealed class CategoryStatus(int id, string name) : Enumeration<CategoryStatus>(id, name)
{
    public static readonly CategoryStatus Draft = new(1, "DRAFT");
    public static readonly CategoryStatus Active = new(2, "ACTIVE");
    public static readonly CategoryStatus Inactive = new(3, "INACTIVE");
    public static readonly CategoryStatus Archived = new(4, "ARCHIVED");

    /// <summary>ARCHIVED is terminal - no further transitions allowed.</summary>
    public bool IsTerminal => this == Archived;

    public bool IsDraft => this == Draft;

    public bool IsActive => this == Active;

    /// <summary>Whether this status can be the source of a Publish transition.</summary>
    public bool CanBePublishedFrom => this == Draft || this == Inactive;

    public bool CanBeActivatedFrom => this == Draft || this == Inactive;

    public bool CanBeDeactivatedFrom => this == Active;

    public bool CanBeArchivedFrom => this == Draft || this == Active || this == Inactive;

    public static bool IsValidTransition(CategoryStatus from, CategoryStatus to)
    {
        if (from == Draft && (to == Active || to == Archived))
        {
            return true;
        }

        if (from == Active && (to == Inactive || to == Archived))
        {
            return true;
        }

        if (from == Inactive && (to == Active || to == Archived))
        {
            return true;
        }

        return false;
    }
}