using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Backend.Middleware;

public class RequestMetricsMiddleware
{
    private static readonly Meter Meter = new("Backend.Api", "1.0");
    private static readonly Counter<long> RequestCount = Meter.CreateCounter<long>("http_requests_total", "requests", "Total HTTP requests");
    private static readonly Histogram<double> RequestDuration = Meter.CreateHistogram<double>("http_request_duration_seconds", "s", "Request duration in seconds");

    private readonly RequestDelegate _next;

    public RequestMetricsMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            var tags = new KeyValuePair<string, object?>[]
            {
                new("method", context.Request.Method),
                new("path", context.Request.Path.Value ?? "-"),
                new("status", context.Response.StatusCode)
            };
            RequestCount.Add(1, tags);
            RequestDuration.Record(stopwatch.Elapsed.TotalSeconds, tags);
        }
    }
}
