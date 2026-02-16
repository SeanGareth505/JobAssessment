using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;


namespace Backend.Middleware;

public class ExceptionProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionProblemDetailsMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ExceptionProblemDetailsMiddleware(RequestDelegate next, ILogger<ExceptionProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation: {Message}", ex.Message);
            var status = ex.Message.Contains("concurrency", StringComparison.OrdinalIgnoreCase)
                ? HttpStatusCode.Conflict
                : HttpStatusCode.BadRequest;
            await WriteProblemDetails(context, status, ex.Message, "InvalidOperation");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteProblemDetails(context, HttpStatusCode.InternalServerError, "An error occurred.", "InternalServerError");
        }
    }

    private static async Task WriteProblemDetails(HttpContext context, HttpStatusCode status, string detail, string title)
    {
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/problem+json";
        var problem = new ProblemDetails
        {
            Type = $"https://httpstatuses.com/{(int)status}",
            Title = title,
            Status = (int)status,
            Detail = detail,
            Instance = context.Request.Path
        };
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
