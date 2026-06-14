using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OsuStocks.Api.Middleware;
using Xunit;

namespace OsuStocks.Api.IntegrationTests;

/// <summary>
/// Deterministic, in-process tests for <see cref="GlobalExceptionHandlerMiddleware"/> — it maps
/// <see cref="DbUpdateConcurrencyException"/> to 409 CONCURRENCY_CONFLICT and any other exception to
/// 500 INTERNAL_ERROR, and passes through when the pipeline does not throw.
///
/// This replaces the old HTTP-racing GlobalExceptionHandlerTests.ConcurrencyConflict test, which
/// could not reliably induce a real row-version conflict through two sequential requests. That a
/// genuine stale row-version actually throws <see cref="DbUpdateConcurrencyException"/> is covered
/// by <c>Persistence/OptimisticConcurrencyRepositoryTests</c>; this verifies the HTTP mapping.
/// </summary>
public sealed class GlobalExceptionHandlerMiddlewareTests
{
    [Fact]
    public async Task DbUpdateConcurrencyException_MapsTo409ConcurrencyConflict()
    {
        var context = CreateContext(traceId: "trace-conflict");
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new DbUpdateConcurrencyException("Simulated concurrency conflict."),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);

        var body = await ReadBodyAsync(context);
        Assert.Equal("CONCURRENCY_CONFLICT", body.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("message").GetString()));
        Assert.Equal("trace-conflict", body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task UnhandledException_MapsTo500InternalError()
    {
        var context = CreateContext(traceId: "trace-boom");
        var middleware = new GlobalExceptionHandlerMiddleware(
            _ => throw new InvalidOperationException("boom"),
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);

        var body = await ReadBodyAsync(context);
        Assert.Equal("INTERNAL_ERROR", body.GetProperty("code").GetString());
        Assert.Equal("trace-boom", body.GetProperty("traceId").GetString());
    }

    [Fact]
    public async Task NoException_PassesThroughWithoutModifyingResponse()
    {
        var context = CreateContext(traceId: "trace-ok");
        var nextCalled = false;
        var middleware = new GlobalExceptionHandlerMiddleware(
            ctx =>
            {
                nextCalled = true;
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            NullLogger<GlobalExceptionHandlerMiddleware>.Instance);

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
    }

    private static DefaultHttpContext CreateContext(string traceId) => new()
    {
        TraceIdentifier = traceId,
        // WriteAsJsonAsync resolves JSON options from RequestServices; an empty provider falls back to defaults.
        RequestServices = new ServiceCollection().BuildServiceProvider(),
        Response = { Body = new MemoryStream() },
    };

    private static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.Clone();
    }
}
