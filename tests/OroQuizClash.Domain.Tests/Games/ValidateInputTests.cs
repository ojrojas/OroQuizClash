using BuildingBlocks.Kernel.Domain.Results;

using OroQuizClash.Domain.Categories;
using OroQuizClash.Domain.Games;
using OroQuizClash.Domain.Games.Enumerations;
using OroQuizClash.Domain.Games.ValueObjects;

namespace OroQuizClash.Domain.Tests.Games;

public sealed class ValidateInputTests
{
    [Fact]
    public void CreateGame_NameTooShort_FailsValidation()
    {
        var categoryId = new CategoryId(Guid.NewGuid());
        var config = new GameConfiguration("AB", categoryId, 5, 10, 1, DifficultyProgressionStrategy.Linear, 30, ScoringSystem.Standard, LossPolicy.LoseCurrentRound, WithdrawalPolicy.KeepCurrentScore, ConsolationPolicy.None, new RewardRules("Points", 1000), 2, 10, 100);
        var result = Game.Create(config, Guid.NewGuid());
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void CreateCategory_NameTooShort_Fails()
    {
        var result = OroQuizClash.Domain.Categories.Category.Create("AB", "desc", new OroQuizClash.Domain.Categories.ValueObjects.KnowledgeArea("Math"), new OroQuizClash.Domain.Categories.ValueObjects.AcademicLevel("Primary"), new OroQuizClash.Domain.Categories.ValueObjects.AgeRange(6, 10), new OroQuizClash.Domain.Categories.ValueObjects.DifficultyLevel(1), new OroQuizClash.Domain.Categories.ValueObjects.CategoryTags([]), new OroQuizClash.Domain.Categories.ValueObjects.PublishConfiguration(false), Guid.NewGuid());
        Assert.True(result.IsFailure);
    }
}
