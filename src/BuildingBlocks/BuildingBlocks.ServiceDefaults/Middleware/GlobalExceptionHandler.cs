namespace BuildingBlocks.ServiceDefaults.Middleware;

/// <summary>
/// Translates unhandled exceptions to RFC 7807 ProblemDetails:
/// validation failures → 400 with per-field errors, domain rule violations → 422,
/// anything else → 500 without leaking internals.
/// Register with services.AddExceptionHandler&lt;GlobalExceptionHandler&gt;() + app.UseExceptionHandler().
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            ValidationException validationException => new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Extensions =
                {
                    ["errors"] = validationException.Failures
                        .GroupBy(failure => failure.PropertyName)
                        .ToDictionary(group => group.Key, group => group.Select(f => f.ErrorMessage).ToArray())
                }
            },
            DomainException domainException => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Domain rule violated",
                Detail = domainException.Message
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred"
            }
        };

        if (problemDetails.Status == StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Path}", httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken).ConfigureAwait(false);

        return true;
    }
}