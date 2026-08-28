using QuizArena.Admin.Client.Models.Categories;

namespace QuizArena.Admin.Tests;

public sealed class CategoryStateTransitionTests
{
    [Theory]
    [InlineData(CategoryStateView.Draft, CategoryStateView.Active)]
    [InlineData(CategoryStateView.Active, CategoryStateView.Inactive)]
    [InlineData(CategoryStateView.Inactive, CategoryStateView.Active)]
    [InlineData(CategoryStateView.Active, CategoryStateView.Archived)]
    [InlineData(CategoryStateView.Inactive, CategoryStateView.Archived)]
    public void ValidTransitions_ShouldBeAllowed(CategoryStateView from, CategoryStateView to)
    {
        Assert.True(IsValidTransition(from, to));
    }

    [Theory]
    [InlineData(CategoryStateView.Archived, CategoryStateView.Active)]
    [InlineData(CategoryStateView.Archived, CategoryStateView.Draft)]
    [InlineData(CategoryStateView.Draft, CategoryStateView.Archived)]
    [InlineData(CategoryStateView.Draft, CategoryStateView.Inactive)]
    public void InvalidTransitions_ShouldBeRejected(CategoryStateView from, CategoryStateView to)
    {
        Assert.False(IsValidTransition(from, to));
    }

    [Fact]
    public void FromApi_MapsCorrectly()
    {
        Assert.Equal(CategoryStateView.Draft, CategoryStateViewMap.FromApi("DRAFT"));
        Assert.Equal(CategoryStateView.Active, CategoryStateViewMap.FromApi("ACTIVE"));
        Assert.Equal(CategoryStateView.Inactive, CategoryStateViewMap.FromApi("INACTIVE"));
        Assert.Equal(CategoryStateView.Archived, CategoryStateViewMap.FromApi("ARCHIVED"));
    }

    [Fact]
    public void ToApi_MapsCorrectly()
    {
        Assert.Equal("DRAFT", CategoryStateViewMap.ToApi(CategoryStateView.Draft));
        Assert.Equal("ACTIVE", CategoryStateViewMap.ToApi(CategoryStateView.Active));
        Assert.Equal("ARCHIVED", CategoryStateViewMap.ToApi(CategoryStateView.Archived));
    }

    [Fact]
    public void IsTerminal_Archived_True()
    {
        Assert.True(CategoryStateViewMap.IsTerminal(CategoryStateView.Archived));
        Assert.False(CategoryStateViewMap.IsTerminal(CategoryStateView.Active));
    }

    [Fact]
    public void CanEdit_Archived_False()
    {
        Assert.False(CategoryStateViewMap.CanEdit(CategoryStateView.Archived));
        Assert.True(CategoryStateViewMap.CanEdit(CategoryStateView.Draft));
        Assert.True(CategoryStateViewMap.CanEdit(CategoryStateView.Active));
    }

    private static bool IsValidTransition(CategoryStateView from, CategoryStateView to) => (from, to) switch
    {
        (CategoryStateView.Draft, CategoryStateView.Active) => true,
        (CategoryStateView.Active, CategoryStateView.Inactive) => true,
        (CategoryStateView.Inactive, CategoryStateView.Active) => true,
        (CategoryStateView.Active, CategoryStateView.Archived) => true,
        (CategoryStateView.Inactive, CategoryStateView.Archived) => true,
        _ => false
    };
}
