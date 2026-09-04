using System.Diagnostics;
using Inventory.Api.Infrastructure.Errors;

namespace Inventory.Api.Infrastructure.Logging;

public class ApiActionLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiActionLogMiddleware> _logger;

    public ApiActionLogMiddleware(RequestDelegate next, ILogger<ApiActionLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApiActionLogService actionLog)
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            await _next(context);
            return;
        }

        var started = DateTime.Now;
        var sw = Stopwatch.StartNew();
        string? error = null;

        try
        {
            await _next(context);
        }
        catch (ApiException ex)
        {
            error = ex.Message;
            context.Response.StatusCode = ex.StatusCode;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new { message = ex.Message, traceId = context.TraceIdentifier });
        }
        catch (Exception ex)
        {
            error = ex.Message;
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json; charset=utf-8";
            await context.Response.WriteAsJsonAsync(new { message = "系統發生未預期錯誤", traceId = context.TraceIdentifier });
            _logger.LogError(ex, "Unhandled API exception. TraceId={TraceId}", context.TraceIdentifier);
        }
        finally
        {
            sw.Stop();
            var endpoint = context.GetEndpoint()?.DisplayName;
            var actor = context.User?.Identity?.IsAuthenticated == true
                ? context.User.Identity?.Name ?? "AUTHENTICATED"
                : context.Request.Headers.TryGetValue("X-Actor", out var xActor) && !string.IsNullOrWhiteSpace(xActor.ToString())
                    ? xActor.ToString()
                    : "ANONYMOUS";
            var ip = context.Connection.RemoteIpAddress?.ToString();
            var userAgent = context.Request.Headers["User-Agent"].ToString();
            var query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : null;

            await actionLog.WriteAsync(new ApiActionLogEntry(
                context.TraceIdentifier,
                context.Request.Method,
                context.Request.Path,
                query,
                endpoint,
                context.Response.StatusCode,
                sw.ElapsedMilliseconds,
                actor,
                ip,
                userAgent,
                error,
                started));

            _logger.LogInformation("API {Method} {Path} => {StatusCode} in {ElapsedMs}ms TraceId={TraceId}",
                context.Request.Method, context.Request.Path, context.Response.StatusCode, sw.ElapsedMilliseconds, context.TraceIdentifier);
        }
    }
}
