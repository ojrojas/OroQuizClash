using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Categories.Rules;

public sealed class CategoryStateTransitionRule(CategoryStatus from, CategoryStatus to) : IBusinessRule
{
    public bool IsBroken()
    {
        // DRAFT -> ACTIVE/INACTIVE/ARCHIVED
        // ACTIVE -> INACTIVE/ARCHIVED
        // INACTIVE -> ACTIVE/ARCHIVED/DRAFT (update)
        // ARCHIVED -> none
        if (from == CategoryStatus.Archived) return true;
        if (from == CategoryStatus.Draft && to == CategoryStatus.Active) return false;
        if (from == CategoryStatus.Draft && to == CategoryStatus.Inactive) return false;
        if (from == CategoryStatus.Draft && to == CategoryStatus.Archived) return false;
        if (from == CategoryStatus.Active && to == CategoryStatus.Inactive) return false;
        if (from == CategoryStatus.Active && to == CategoryStatus.Archived) return false;
        if (from == CategoryStatus.Inactive && to == CategoryStatus.Active) return false;
        if (from == CategoryStatus.Inactive && to == CategoryStatus.Draft) return false;
        if (from == CategoryStatus.Inactive && to == CategoryStatus.Archived) return false;
        return true;
    }
    public string Message => $"Invalid category state transition from {from.Name} to {to.Name}.";
}