using BuildingBlocks.Kernel.Domain.Results;

namespace BuildingBlocks.ServiceDefaults.Endpoints;

/// <summary>
/// Maps domain <see cref="Result"/>/<see cref="Result{TValue}"/> to Minimal API responses,
/// translating <see cref="ErrorType"/> to the proper status code as ProblemDetails.
/// </summary>
public static class ResultExtensions
{
    public static IResult ToHttpResult(this Result result) =>
        result.IsSuccess ? Results.NoContent() : ToProblem(result.Error);

    public static IResult ToHttpResult<TValue>(this Result<TValue> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : ToProblem(result.Error);

    public static IResult ToCreatedResult<TValue>(this Result<TValue> result, Func<TValue, string> locationFactory) =>
        result.IsSuccess ? Results.Created(locationFactory(result.Value), result.Value) : ToProblem(result.Error);

    private static IResult ToProblem(Error error)
    {
        var statusCode = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status500InternalServerError
        };

        return Results.Problem(
            title: error.Code,
            detail: error.Description,
            statusCode: statusCode);
    }
}