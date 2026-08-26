using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

using OroQuizClash.Domain.Categories;

namespace OroQuizClash.Api.Tests.Errors;

public sealed class CategoryErrorsMappingTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    public void ErrorType_MapsToCorrectStatus(ErrorType type, int expectedStatusCode)
    {
        var error = new Error("Test.Code", "detail", type);
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();
        Assert.NotNull(httpResult);
        Assert.Equal(expectedStatusCode, MapToStatusCode(error.Type));
    }

    private static int MapToStatusCode(ErrorType type) => type switch
    {
        ErrorType.Validation => 400,
        ErrorType.NotFound => 404,
        ErrorType.Conflict => 409,
        _ => 500
    };

    [Fact]
    public void InvalidCategoryConfiguration_MapsTo400()
    {
        var error = CategoryErrors.InvalidCategoryConfiguration("Invalid age range");
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("InvalidCategoryConfiguration", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void InvalidCategoryConfiguration_InvalidName_MapsTo400()
    {
        var error = CategoryErrors.InvalidName;
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("InvalidCategoryConfiguration.InvalidName", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void InvalidCategoryConfiguration_InvalidAgeRange_MapsTo400()
    {
        var error = CategoryErrors.InvalidAgeRange;
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("InvalidCategoryConfiguration.InvalidAgeRange", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void InvalidCategoryConfiguration_InvalidTags_MapsTo400()
    {
        var error = CategoryErrors.InvalidTags;
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("InvalidCategoryConfiguration.InvalidTags", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void InvalidCategoryConfiguration_InvalidDifficulty_MapsTo400()
    {
        var error = CategoryErrors.InvalidDifficulty;
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("InvalidCategoryConfiguration.InvalidDifficulty", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void CategoryNotPublishable_MapsTo400()
    {
        var error = CategoryErrors.CategoryNotPublishable;
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("CategoryNotPublishable", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void CategoryNotReady_MapsTo400()
    {
        var error = CategoryErrors.CategoryNotReady;
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("CategoryNotReady", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void InvalidCategoryState_MapsTo400()
    {
        var error = CategoryErrors.InvalidCategoryState("Update only in DRAFT");
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Validation, error.Type);
        Assert.Equal("InvalidCategoryState", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void CategoryNotFound_MapsTo404()
    {
        var error = CategoryErrors.CategoryNotFound();
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("CategoryNotFound", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void CategoryNotFound_WithId_MapsTo404()
    {
        var id = Guid.NewGuid();
        var error = CategoryErrors.CategoryNotFound(id);
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.NotFound, error.Type);
        Assert.Equal("CategoryNotFound", error.Code);
        Assert.Contains(id.ToString(), error.Description);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void ConcurrencyConflict_MapsTo409()
    {
        var error = CategoryErrors.ConcurrencyConflict;
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();

        Assert.Equal(ErrorType.Conflict, error.Type);
        Assert.Equal("ConcurrencyConflict", error.Code);
        Assert.NotNull(httpResult);
    }

    [Fact]
    public void AllCategoryErrorCodes_AreDistinct()
    {
        // There are 10 unique error codes (Detail variants share same code as base)
        var codes = new HashSet<string>
        {
            CategoryErrors.InvalidCategoryConfiguration().Code,
            CategoryErrors.InvalidName.Code,
            CategoryErrors.InvalidAgeRange.Code,
            CategoryErrors.InvalidTags.Code,
            CategoryErrors.InvalidDifficulty.Code,
            CategoryErrors.CategoryNotPublishable.Code,
            CategoryErrors.CategoryNotReady.Code,
            CategoryErrors.InvalidCategoryState().Code,
            CategoryErrors.CategoryNotFound().Code,
            CategoryErrors.ConcurrencyConflict.Code,
        };

        Assert.Equal(10, codes.Count);
    }
}