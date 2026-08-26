using BuildingBlocks.Kernel.Domain.Rules;

namespace OroQuizClash.Domain.Categories.Rules;

public sealed class CategoryTagsValidRule(IEnumerable<string> tags) : IBusinessRule
{
    public bool IsBroken()
    {
        var list = tags?.ToList() ?? new List<string>();
        if (list.Count > 10) return true;
        foreach (var t in list)
        {
            var trimmed = t?.Trim() ?? "";
            if (trimmed.Length < 2 || trimmed.Length > 30) return true;
        }
        return false;
    }
    public string Message => "Tags invalid: max 10 tags, each 2-30 characters, lowercased and deduplicated.";
}