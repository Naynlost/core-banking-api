using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Extensions;

/// <summary>
/// Maps failed <see cref="Banking.Domain.Primitives.Result"/> error codes to HTTP
/// problem responses: business rule violations are expected outcomes, not exceptions,
/// so they surface as 4xx ProblemDetails with the machine-readable code attached.
/// </summary>
public static class ResultExtensions
{
    public static IActionResult FailureProblem(this ControllerBase controller, string errorCode)
    {
        var statusCode = errorCode switch
        {
            _ when errorCode.EndsWith("not_found", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            _ when errorCode.EndsWith("conflict", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        return controller.Problem(
            title: "The request could not be processed.",
            detail: errorCode,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["code"] = errorCode });
    }
}
