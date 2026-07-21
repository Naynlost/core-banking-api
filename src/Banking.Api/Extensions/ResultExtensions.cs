using Microsoft.AspNetCore.Mvc;

namespace Banking.Api.Extensions;

// Başarısız Result hata kodlarını 4xx ProblemDetails'e çevirir, kod extension'da tutulur
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
