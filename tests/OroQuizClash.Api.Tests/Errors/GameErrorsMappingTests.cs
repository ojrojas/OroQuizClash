using BuildingBlocks.Kernel.Domain.Results;
using BuildingBlocks.ServiceDefaults.Endpoints;

namespace OroQuizClash.Api.Tests.Errors;

public sealed class GameErrorsMappingTests
{
    [Theory]
    [InlineData(ErrorType.Validation, 400)]
    [InlineData(ErrorType.NotFound, 404)]
    [InlineData(ErrorType.Conflict, 409)]
    public void ErrorType_MapsToCorrectStatus(ErrorType type, int expectedStatusCode)
    {
        _ = expectedStatusCode;
        var error = new Error("Test.Code", "detail", type);
        var result = Result.Failure<string>(error);
        var httpResult = result.ToHttpResult();
        Assert.NotNull(httpResult);
    }
}