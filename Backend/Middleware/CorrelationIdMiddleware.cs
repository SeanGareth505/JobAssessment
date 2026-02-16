using Microsoft.Extensions.Logging;

namespace Backend.Middleware;

public class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const string CorrelationIdItemKey = "CorrelationId";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString("N");
        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers.Append(CorrelationIdHeader, correlationId);
            return Task.CompletedTask;
        });
        using (_logger.BeginScope("CorrelationId={CorrelationId}", correlationId))
        {
            await _next(context);
        }
    }
}
