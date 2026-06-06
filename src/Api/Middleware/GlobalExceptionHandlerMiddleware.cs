using System.Net;

namespace OsuStocks.Api.Middleware;

internal sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next,
    ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await next(httpContext);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex,
                "Concurrency conflict detected for TraceId {TraceId}",
                httpContext.TraceIdentifier);

            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                code = "CONCURRENCY_CONFLICT",
                message = "The resource was modified by another request. Please retry.",
                traceId = httpContext.TraceIdentifier
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Unhandled exception for TraceId {TraceId}",
                httpContext.TraceIdentifier);

            httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                code = "INTERNAL_ERROR",
                message = "An unexpected error occurred.",
                traceId = httpContext.TraceIdentifier
            });
        }
    }
}
