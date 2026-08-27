using BuildingBlocks.Kernel.Domain.Specifications;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;

namespace OroQuizClash.Infrastructure.Specifications;

public sealed class GameByIdSpecification : Specification<Game>
{
    public GameByIdSpecification(GameId id)
    {
        Where(g => g.Id == id);
        // Include Players and Rounds for lifecycle operations
        AddInclude(g => g.Players);
        AddInclude(g => g.Rounds);
    }

    public GameByIdSpecification(Guid id) : this(new GameId(id)) { }
}

public sealed class GameFilterSpecification : Specification<Game>
{
    public GameFilterSpecification(
        string? status = null,
        Guid? categoryId = null,
        Guid? createdBy = null,
        string? search = null,
        int page = 1,
        int pageSize = 20,
        bool paginate = true)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            try
            {
                var st = GameStatus.FromName(status.Trim());
                Where(g => g.Status == st);
            }
            catch (ArgumentOutOfRangeException)
            {
                Where(g => false);
            }
        }

        if (categoryId.HasValue && categoryId.Value != Guid.Empty)
        {
            var catId = new CategoryId(categoryId.Value);
            Where(g => g.Configuration.CategoryId == catId);
        }

        if (createdBy.HasValue && createdBy.Value != Guid.Empty)
        {
            var uid = createdBy.Value;
            Where(g => g.CreatedBy == uid);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            Where(g => g.Name.Contains(term));
        }

        ApplyAsNoTracking();
        ApplyOrderByDescending(g => (object)g.CreatedAt);

        if (paginate)
        {
            var safePage = page < 1 ? 1 : page;
            var safeSize = pageSize < 1 ? 20 : pageSize > 100 ? 100 : pageSize;
            var skip = (safePage - 1) * safeSize;
            ApplyPaging(skip, safeSize);
        }
    }

    public static GameFilterSpecification ForCount(
        string? status = null,
        Guid? categoryId = null,
        Guid? createdBy = null,
        string? search = null) =>
        new(status, categoryId, createdBy, search, 1, 20, paginate: false);
}
