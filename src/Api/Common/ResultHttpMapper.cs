using AppError = OsuStocks.Application.Common.Models.Error;

namespace OsuStocks.Api.Common;

internal static class ResultHttpMapper
{
    public static Microsoft.AspNetCore.Http.IResult ToErrorResult(this AppError error, HttpContext httpContext)
    {
        var statusCode = error.Code switch
        {
            "VALIDATION_ERROR" => StatusCodes.Status400BadRequest,
            "INVALID_STATE" => StatusCodes.Status400BadRequest,
            "UNAUTHORIZED" => StatusCodes.Status401Unauthorized,
            "NOT_FOUND" => StatusCodes.Status404NotFound,
            "CONFLICT" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Json(new
        {
            code = error.Code,
            message = error.Message,
            traceId = httpContext.TraceIdentifier
        }, statusCode: statusCode);
    }
}
