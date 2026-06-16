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
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            // Generic backstop: a unique-constraint violation that escaped a handler means the request
            // conflicts with existing state. (The daily-reward flow translates its own duplicate to an
            // idempotent success and never reaches here.)
            logger.LogWarning(ex,
                "Unique constraint violation for TraceId {TraceId}",
                httpContext.TraceIdentifier);

            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                code = "CONFLICT",
                message = "The request conflicts with the current state of the resource.",
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
